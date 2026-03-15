using IgnakeeAI.McpServer.Supplier.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration
{
    /// <summary>
    /// Configuración del proveedor leída de variables de entorno.
    /// Centraliza el acceso para no repetir Environment.GetEnvironmentVariable en cada tool.
    /// /// Soporta tanto variables de entorno (SUPPLIER_*) como claves en appsettings,
    /// ya que IConfiguration unifica ambas fuentes automáticamente en ASP.NET Core.
    /// </summary>
    public class SupplierConfig : ISupplierConfig
    {
        private readonly IConfiguration _configuration;

        public SupplierConfig(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string ContactEmail => _configuration["SUPPLIER_CONTACT_EMAIL"]
            ?? Environment.GetEnvironmentVariable("SUPPLIER_CONTACT_EMAIL")
            ?? "Correo Desconocido";
        public string ContactPhone => _configuration["SUPPLIER_CONTACT_PHONE"]
            ?? Environment.GetEnvironmentVariable("SUPPLIER_CONTACT_PHONE")
            ?? "Teléfono Desconocido";
        public string ContactAddress => _configuration["SUPPLIER_CONTACT_ADDRESS"]
            ?? Environment.GetEnvironmentVariable("SUPPLIER_CONTACT_ADDRESS")
            ?? "Calle Desconocida, Ciudad Desconocida";
        public string VendorName =>
            _configuration["SUPPLIER_VENDOR_NAME"]
            ?? Environment.GetEnvironmentVariable("SUPPLIER_VENDOR_NAME")
            ?? "Proveedor IgnakeeAI";

        public string BusinessHours =>
            _configuration["SUPPLIER_BUSINESS_HOURS"]
            ?? Environment.GetEnvironmentVariable("SUPPLIER_BUSINESS_HOURS")
            ?? "Lun-Vie 08:00-18:00";
    }
}
