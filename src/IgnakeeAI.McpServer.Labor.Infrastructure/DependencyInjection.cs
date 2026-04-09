using IgnakeeAI.McpServer.Labor.Application.Interfaces;
using IgnakeeAI.McpServer.Labor.Application.Services;
using IgnakeeAI.McpServer.Labor.Infrastructure.Configuration;
using IgnakeeAI.McpServer.Labor.Infrastructure.Persistence;
using IgnakeeAI.McpServer.Labor.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IgnakeeAI.McpServer.Labor.Infrastructure
{
    /// <summary>
    /// Extensión para registrar todos los servicios de Infrastructure y Application
    /// en el contenedor de dependencias. Centraliza la configuración para mantener
    /// Program.cs limpio y desacoplado de los detalles de infraestructura.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddLaborInfrastructure(
            this IServiceCollection services, IConfiguration configuration)
        {
            // ── Base de datos ────────────────────────────────────────────────────────
            var provider = configuration["DatabaseProvider"]?.ToLowerInvariant() ?? "sqlite";
            var connectionString = configuration.GetConnectionString("Labor")
                ?? "Data Source=labor.db";

            services.AddDbContext<LaborDbContext>(options =>
            {
                _ = provider switch
                {
                    "sqlite" => options.UseSqlite(connectionString),
                    "postgresql" or "postgres" => options.UseNpgsql(connectionString),
                    "sqlserver" => options.UseSqlServer(connectionString),
                    "mysql" => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)),
                    _ => options.UseSqlite(connectionString)
                };
            });

            // ── Repositorio (puerto → adaptador) ────────────────────────────────────
            services.AddScoped<IWorkerRepository, EfWorkerRepository>();

            // ── Configuración de la agencia ─────────────────────────────────────────
            services.AddSingleton<ILaborConfig, LaborConfig>();

            // ── Servicio de aplicación ───────────────────────────────────────────────
            services.AddScoped<WorkerSearchService>();

            return services;
        }
    }
}
