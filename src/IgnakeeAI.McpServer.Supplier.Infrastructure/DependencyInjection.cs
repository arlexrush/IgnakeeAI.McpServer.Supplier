using IgnakeeAI.McpServer.Supplier.Application.Interfaces;
using IgnakeeAI.McpServer.Supplier.Application.Contracts;
using IgnakeeAI.McpServer.Supplier.Application.Services;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Erp;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore; // Agrega esta directiva using para habilitar UseSqlServer, UseSqlite, UseNpgsql, UseMySql
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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
            this IServiceCollection services, IConfiguration configuration,
            IHostEnvironment environment)
        {
            // ── Base de datos ────────────────────────────────────────────────────────
            var configuredProvider = configuration["DatabaseProvider"]?.Trim();
            var provider = string.IsNullOrWhiteSpace(configuredProvider)
                ? environment.IsProduction() ? "" : "sqlite"
                : configuredProvider.ToLowerInvariant();

            if (environment.IsProduction() && provider is not ("postgresql" or "postgres"))
            {
                throw new InvalidOperationException(
                    "En producción DatabaseProvider debe ser 'postgresql' o 'postgres'. " +
                    "No se permite iniciar producción con SQLite.");
            }

            var connectionString = configuration.GetConnectionString("Catalog")
                ?? (environment.IsProduction() ? "" : "Data Source=catalog.db");

            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:Catalog es obligatoria para el proveedor de base de datos configurado.");
            }

            // Configura el DbContext con el proveedor seleccionado dinámicamente
            services.AddDbContext<SupplierCatalogDbContext>(options =>
            {
                _ = provider switch
                {
                    "sqlite" => options.UseSqlite(connectionString), // Opción por defecto, ideal para desarrollo y pruebas, fácil de configurar y sin necesidad de un servidor de base de datos.
                    "postgresql" or "postgres" => options.UseNpgsql(connectionString), // Excelente para producción, especialmente en entornos Linux, con buen rendimiento y soporte para características avanzadas.
                    "sqlserver" => options.UseSqlServer(connectionString), // Opción común en entornos Windows, con integración nativa en el ecosistema Microsoft, pero puede ser más pesado y costoso que otras opciones.
                    "mysql" => options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)), // Popular en entornos web, especialmente con PHP, buena compatibilidad y rendimiento decente.
                    _ => throw new InvalidOperationException(
                        $"Proveedor de base de datos no soportado: '{provider}'.")
                };
            });

            // ── Repositorio (puerto → adaptador) ────────────────────────────────────
            services.AddScoped<ICatalogRepository, EfCatalogRepository>();
            services.AddScoped<CatalogSyncAuditWriter>();

            // ── Configuración del proveedor ─────────────────────────────────────────
            services.AddSingleton<ISupplierConfig, SupplierConfig>();
            services.AddOptions<SupplierLocation>()
                .Bind(configuration.GetSection("Supplier:Location"))
                .Validate(location =>
                {
                    var hasLatitude = location.Latitude.HasValue;
                    var hasLongitude = location.Longitude.HasValue;
                    var coordinatesAreValid = hasLatitude && hasLongitude &&
                        location.Latitude is >= -90 and <= 90 &&
                        location.Longitude is >= -180 and <= 180;

                    return (!hasLatitude && !hasLongitude || coordinatesAreValid) &&
                        (!location.IsValidated || coordinatesAreValid);
                },
                "Supplier:Location requiere coordenadas válidas juntas; IsValidated=true también requiere coordenadas.")
                .ValidateOnStart();

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
