using IgnakeeAI.McpServer.Labor.Infrastructure;
using IgnakeeAI.McpServer.Labor.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IgnakeeAI.McpServer.Labor.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // ── 1. Infraestructura (BD, repositorios, servicios) ──────────────────────
            builder.Services.AddLaborInfrastructure(builder.Configuration);

            // ── 2. Servidor MCP con transporte HTTP ──────────────────────────────────────
            builder.Services.AddMcpServer()
                .WithToolsFromAssembly(typeof(IgnakeeAI.McpServer.Labor.McpTools.WorkerRateTools).Assembly)
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

            // ── Migración automática de la BD ────────────────────────────────────────
            var applyMigrationsOnStartup = builder.Configuration.GetValue("Database:ApplyMigrationsOnStartup", true);
            if (applyMigrationsOnStartup)
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LaborDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                var migrationStrategy = builder.Configuration.GetValue<string>("Database:MigrationStrategy", "auto");

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
                            var delaySeconds = Math.Min(Math.Pow(2, i), 8);
                            logger.LogWarning(ex, "Fallo migración intento {Attempt}/{Max}. Reintentando...", i, maxAttempts);
                            await Task.Delay(TimeSpan.FromSeconds(delaySeconds));
                        }
                        catch (Exception ex)
                        {
                            logger.LogCritical(ex, "Fallo definitivo en migración. El servidor arrancará sin migrar.");
                        }
                    }
                }
            }

            app.UseCors();
            app.MapMcp("/mcp");
            app.MapHealthChecks("/health");

            var version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "1.0.0";

            app.MapGet("/", () => Results.Ok(new
            {
                server = "IgnakeeAI MCP Labor Server",
                version,
                mcp_endpoint = "/mcp",
                tools = new[] { "getWorkerRate", "searchWorkers", "getWorkerProfile", "checkWorkerAvailability", "getContactInfo" }
            }));

            app.Run();
        }
    }
}
