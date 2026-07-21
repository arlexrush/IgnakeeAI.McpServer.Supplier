using IgnakeeAI.McpServer.Supplier.Infrastructure;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using IgnakeeAI.McpServer.Supplier.Application.Contracts;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration;
using IgnakeeAI.McpServer.Supplier.Api.Middleware;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Text.Json;
// Agregar los siguientes using para los tipos de autenticación personalizados
using IgnakeeAI.McpServer.Supplier.Api.Security; // Ajusta el namespace según donde estén definidos los tipos


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

            // ── 3. Health checks ─────────────────────────────────────────────────────
            builder.Services.AddHealthChecks();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/error");
            }

            // ── Migración automática de la BD (controlada por configuración) ─────────
            var applyMigrationsOnStartup = builder.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true);
            if (applyMigrationsOnStartup)
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<SupplierCatalogDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                var migrationStrategy = builder.Configuration.GetValue<string>("Database:MigrationStrategy", "auto"); // auto | manual | skip

                if (migrationStrategy == "auto")
                {
                    var maxAttempts = 5;
                    for (int i = 1; i <= maxAttempts; i++)
                    {
                        try
                        {
                            await db.Database.MigrateAsync();
                            logger.LogInformation("Migraciones aplicadas correctamente.");
                            break;
                        }
                        catch (Exception ex) when (i < maxAttempts)
                        {
                            logger.LogWarning(ex, "Fallo migración intento {Attempt}/{Max}. Reintentando...", i, maxAttempts);
                            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, i)));
                        }
                        catch (Exception ex)
                        {
                            logger.LogCritical(ex, "Fallo definitivo en migración. El servidor arrancará sin migrar.");
                            // NO re-throw: permitir que el servidor arranque para healthchecks
                        }
                    }
                }
            }

            // Configuración de middlewares y endpoints
            app.UseCors("SupplierApiCors");
            app.UseMiddleware<LegioCorrelationMiddleware>();
            app.UseAuthentication();
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
