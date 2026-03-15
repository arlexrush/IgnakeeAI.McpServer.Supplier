using IgnakeeAI.McpServer.Supplier.Infrastructure;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;


namespace IgnakeeAI.McpServer.Supplier.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ── 1. Infraestructura (BD, repositorios, servicios, conectores) ─────────
            builder.Services.AddInfrastructure(builder.Configuration);

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

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
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

                try
                {
                    await db.Database.MigrateAsync();
                    logger.LogInformation("Migraciones de base de datos aplicadas correctamente.");
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Error aplicando migraciones de base de datos al inicio.");
                    throw;
                }
            }

            // Configuración de middlewares y endpoints
            app.UseCors();
            app.MapMcp("/mcp"); // Endpoint principal del servidor MCP
            app.MapHealthChecks("/health");

            var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";

            // Endpoint raíz para información básica del servidor
            app.MapGet("/", () => Results.Ok(new
            {
                server = "IgnakeeAI MCP Supplier Server",
                version,
                mcp_endpoint = "/mcp",
                tools = new[] { "getPrice", "searchAlternatives", "checkAvailability", "getBusinessHours" }
            }));

            app.MapAdminCatalogEndpoints();

            app.Run();

        }

        
    }
}
