using IgnakeeAI.McpServer.Supplier.Api.Middleware;
// Agregar los siguientes using para los tipos de autenticación personalizados
using IgnakeeAI.McpServer.Supplier.Api.Security; // Ajusta el namespace según donde estén definidos los tipos
using IgnakeeAI.McpServer.Supplier.Application.Contracts;
using IgnakeeAI.McpServer.Supplier.Infrastructure;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;



namespace IgnakeeAI.McpServer.Supplier.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ── 1. Infraestructura (BD, repositorios, servicios, conectores) ─────────
            builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
            builder.Services
                .AddOptions<SupplierMcpOptions>()
                .Bind(builder.Configuration.GetSection(SupplierMcpOptions.SectionName))
                .Validate(
                    options => !string.IsNullOrWhiteSpace(options.ContractVersion),
                    "Mcp:ContractVersion es obligatorio.")
                .ValidateOnStart();

            // ── 2. Servidor MCP con transporte HTTP ──────────────────────────────────────
            //builder.Services.AddMcpServer()                
            //    .WithToolsFromAssembly(typeof(IgnakeeAI.McpServer.Supplier.McpTools.PricingTools).Assembly)
            //    .WithHttpTransport();
            builder.Services.AddMcpServer()
                .WithToolsFromAssembly(typeof(IgnakeeAI.McpServer.Supplier.McpTools.PricingTools).Assembly)
                .WithHttpTransport(options =>
                {
                    options.Stateless = true;
                });

            builder.Services.AddAuthentication("ApiKey")
            .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>("ApiKey", opts =>
            {
                opts.AdminApiKey = builder.Configuration["Admin:ApiKey"] ?? string.Empty;
                opts.Clients = builder.Configuration.GetSection("Mcp:Clients")
                    .GetChildren()
                    .Select(clientSection => new ApiKeyClientOptions
                    {
                        ClientId = clientSection["ClientId"] ?? string.Empty,
                        ApiKey = clientSection["ApiKey"] ?? string.Empty,
                        Scopes = clientSection.GetSection("Scopes")
                            .GetChildren()
                            .Select(scope => scope.Value)
                            .Where(scope => !string.IsNullOrWhiteSpace(scope))
                            .Cast<string>()
                            .ToArray()
                    })
                    .Where(client => !string.IsNullOrWhiteSpace(client.ClientId) &&
                                     !string.IsNullOrWhiteSpace(client.ApiKey))
                    .ToArray();

                if (opts.Clients.Any(client => client.ApiKey == opts.AdminApiKey))
                    throw new InvalidOperationException(
                        "Admin:ApiKey y las claves MCP deben ser diferentes.");
            });

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy("McpReadPolicy", policy =>
                {
                    policy.AddAuthenticationSchemes("ApiKey");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("scope", "catalog.read");
                });

                options.AddPolicy("McpAvailabilityPolicy", policy =>
                {
                    policy.AddAuthenticationSchemes("ApiKey");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("scope", "availability.read");
                });

                options.AddPolicy("SupplierAdminPolicy", policy =>
                {
                    policy.AddAuthenticationSchemes("ApiKey");
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim("role", "supplier-admin");
                });
            });

            builder.Services.AddCors(options =>
            {
                var origins = builder.Configuration
                    .GetSection("Cors:AllowedOrigins")
                    .GetChildren()
                    .Select(section => section.Value)
                    .Where(origin => !string.IsNullOrWhiteSpace(origin))
                    .Cast<string>()
                    .ToArray();

                options.AddPolicy("SupplierApiCors", policy =>
                {
                    policy.WithOrigins(origins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                });
            });



            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = CatalogUploadLimits.MaxRequestBytes;
            });


            var adminImportPermitLimit = Math.Max(
                1,
                builder.Configuration.GetValue<int>("RateLimiting:AdminFileImport:PermitLimit", 3));

            builder.Services.AddRateLimiter(options =>
            {
                options.OnRejected = static (context, _) =>
                {
                    context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    return ValueTask.CompletedTask;
                };

                options.AddPolicy("AdminFileImport", context =>
                {
                    var clientId = context.User.FindFirst("client_id")?.Value ?? "anonymous";

                    return RateLimitPartition.GetFixedWindowLimiter(
                        clientId,
                        _ => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = adminImportPermitLimit,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        });
                });
            });

            // ── 3. Health checks ─────────────────────────────────────────────────────
            builder.Services.AddHealthChecks();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler(exceptionApp =>
                {
                    exceptionApp.Run(context =>
                        Results.Problem(
                            statusCode: StatusCodes.Status500InternalServerError,
                            title: "Error interno del servidor",
                            detail: "Se produjo un error inesperado al procesar la solicitud.")
                            .ExecuteAsync(context));
                });
            }

            // Las migraciones se aplican mediante un job de despliegue único,
            // antes de iniciar o escalar las réplicas de la API.

            // Configuración de middlewares y endpoints
            app.UseCors("SupplierApiCors");
            app.UseMiddleware<LegioCorrelationMiddleware>();
            app.UseAuthentication();
            app.UseRateLimiter();
            app.UseAuthorization();
            // Se mantiene temporalmente la autorización a nivel de transporte.
            // La granularidad por herramienta se habilitará con el filtro de autorización
            // específico de la versión MCP instalada.
            app.MapMcp("/mcp")
                .RequireAuthorization("McpReadPolicy"); // Endpoint principal del servidor MCP
            app.MapGet("/health", async (
                SupplierCatalogDbContext db,
                IOptions<SupplierMcpOptions> mcpOptions,
                ILogger<Program> logger,
                CancellationToken cancellationToken) =>
            {
                var databaseStatus = "Healthy";
                var catalogProducts = 0;
                var migrationsStatus = "Healthy";

                try
                {
                    if (!await db.Database.CanConnectAsync(cancellationToken))
                    {
                        databaseStatus = "Unhealthy";
                    }
                    else
                    {
                        catalogProducts = await db.Products.CountAsync(cancellationToken);
                        var pendingMigrations = (await db.Database
                            .GetPendingMigrationsAsync(cancellationToken)).ToList();
                        if (pendingMigrations.Count > 0)
                            migrationsStatus = "Unhealthy";
                    }
                }
                catch (Exception ex)
                {
                    databaseStatus = "Unhealthy";
                    migrationsStatus = "Unknown";
                    // No registrar la excepción: algunos proveedores pueden incluir
                    // detalles sensibles de conexión en el mensaje.
                    logger.LogWarning("Health check del catálogo no disponible: {ExceptionType}.",
                        ex.GetType().Name);
                }

                var toolsRegistered = SupplierMcpToolNames.All.Count == 4;
                var mcpStatus = toolsRegistered ? "Healthy" : "Unhealthy";
                var isHealthy = databaseStatus == "Healthy" &&
                                migrationsStatus == "Healthy" &&
                                mcpStatus == "Healthy";

                var response = new
                {
                    status = isHealthy ? "Healthy" : "Unhealthy",
                    server = mcpOptions.Value.ServerName,
                    version = mcpOptions.Value.ServerVersion,
                    contractVersion = mcpOptions.Value.ContractVersion,
                    mcp = mcpStatus,
                    toolsRegistered,
                    database = databaseStatus,
                    migrations = migrationsStatus,
                    catalogProducts,
                    timestamp = DateTimeOffset.UtcNow
                };

                return isHealthy
                    ? Results.Ok(response)
                    : Results.Json(response, statusCode: StatusCodes.Status503ServiceUnavailable);
            });

            var assemblyVersion = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";
            var mcpOptions = app.Services.GetRequiredService<IOptions<SupplierMcpOptions>>().Value;
            var serverVersion = string.IsNullOrWhiteSpace(mcpOptions.ServerVersion)
                ? assemblyVersion
                : mcpOptions.ServerVersion;

            // Endpoint raíz para información básica del servidor
            app.MapGet("/", () => Results.Ok(new
            {
                server = mcpOptions.ServerName,
                version = serverVersion,
                contractVersion = mcpOptions.ContractVersion,
                protocolVersion = string.IsNullOrWhiteSpace(mcpOptions.ProtocolVersion)
                    ? null
                    : mcpOptions.ProtocolVersion,
                mcpEndpoint = "/mcp",
                healthEndpoint = "/health",
                tools = SupplierMcpToolNames.All.ToArray()
            }));

            app.MapAdminCatalogEndpoints();

            app.Run();

        }

        
    }
}
