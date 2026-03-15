using IgnakeeAI.McpServer.Supplier.Application.Interfaces;
using IgnakeeAI.McpServer.Supplier.Application.Services;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Erp;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore; // Agrega esta directiva using para habilitar UseSqlServer, UseSqlite, UseNpgsql, UseMySql
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure
{
    /// <summary>
    /// Extensión para registrar todos los servicios de Infrastructure y Application
    /// en el contenedor de dependencias. Centraliza la configuración para mantener
    /// Program.cs limpio y desacoplado de los detalles de infraestructura.
    /// </summary>
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(
            this IServiceCollection services, IConfiguration configuration)
        {
            // ── Base de datos ────────────────────────────────────────────────────────
            var provider = configuration["DatabaseProvider"]?.ToLowerInvariant() ?? "sqlite"; // Lee el proveedor de base de datos desde la configuración, con "sqlite" como opción por defecto para facilitar el desarrollo y pruebas sin necesidad de configurar un servidor de base de datos.
            var connectionString = configuration.GetConnectionString("Catalog")
                ?? "Data Source=catalog.db";

            // Configura el DbContext con el proveedor seleccionado dinámicamente
            services.AddDbContext<SupplierCatalogDbContext>(options =>
            {
                _ = provider switch
                {
                    "sqlite" => options.UseSqlite(connectionString), // Opción por defecto, ideal para desarrollo y pruebas, fácil de configurar y sin necesidad de un servidor de base de datos.
                    "postgresql" or "postgres" => options.UseNpgsql(connectionString), // Excelente para producción, especialmente en entornos Linux, con buen rendimiento y soporte para características avanzadas.
                    "sqlserver" => options.UseSqlServer(connectionString), // Opción común en entornos Windows, con integración nativa en el ecosistema Microsoft, pero puede ser más pesado y costoso que otras opciones.
                    "mysql" => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)), // Popular en entornos web, especialmente con PHP, buena compatibilidad y rendimiento decente.
                    _ => options.UseSqlite(connectionString)
                };
            });

            // ── Repositorio (puerto → adaptador) ────────────────────────────────────
            services.AddScoped<ICatalogRepository, EfCatalogRepository>();

            // ── Configuración del proveedor ─────────────────────────────────────────
            services.AddSingleton<ISupplierConfig, SupplierConfig>();

            // ── Servicio de aplicación ───────────────────────────────────────────────
            services.AddScoped<CatalogSearchService>();

            // ── Conectores de importación (opcionales) ──────────────────────────────
            services.AddScoped<ExcelCatalogConnector>();
            services.AddScoped<CsvCatalogConnector>();

            // ── Conectores ERP (opcionales, según configuración) ────────────────────
            // Asegúrate de registrar el HttpClientFactory  
            var erpProvider = configuration["Erp:Provider"]?.ToLowerInvariant();
            if (erpProvider == "odoo")
            {
                services.Configure<DataSourceSettings>(configuration.GetSection("Erp:Odoo"));
                services.AddHttpClient<IErpConnector, OdooConnector>();
            }
            else if (erpProvider == "sap")
            {
                services.Configure<DataSourceSettings>(configuration.GetSection("Erp:Sap"));
                services.AddHttpClient<IErpConnector, SapConnector>();
            }                        

            return services;
        }
    }
}
