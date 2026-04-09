using IgnakeeAI.McpServer.Labor.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace IgnakeeAI.McpServer.Labor.Infrastructure.Configuration
{
    /// <summary>
    /// Configuración de la agencia de mano de obra leída de variables de entorno / appsettings.
    /// Soporta tanto variables de entorno (LABOR_*) como claves en appsettings,
    /// ya que IConfiguration unifica ambas fuentes automáticamente en ASP.NET Core.
    /// </summary>
    public class LaborConfig : ILaborConfig
    {
        private readonly IConfiguration _configuration;

        public LaborConfig(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string AgencyName =>
            _configuration["LABOR_AGENCY_NAME"]
            ?? Environment.GetEnvironmentVariable("LABOR_AGENCY_NAME")
            ?? "Agencia de Mano de Obra IgnakeeAI";

        public string ContactEmail =>
            _configuration["LABOR_CONTACT_EMAIL"]
            ?? Environment.GetEnvironmentVariable("LABOR_CONTACT_EMAIL")
            ?? "contacto@mano-de-obra.local";

        public string ContactPhone =>
            _configuration["LABOR_CONTACT_PHONE"]
            ?? Environment.GetEnvironmentVariable("LABOR_CONTACT_PHONE")
            ?? "Teléfono Desconocido";

        public string ContactAddress =>
            _configuration["LABOR_CONTACT_ADDRESS"]
            ?? Environment.GetEnvironmentVariable("LABOR_CONTACT_ADDRESS")
            ?? "Dirección Desconocida";

        public string BusinessHours =>
            _configuration["LABOR_BUSINESS_HOURS"]
            ?? Environment.GetEnvironmentVariable("LABOR_BUSINESS_HOURS")
            ?? "Lun-Vie 08:00-18:00";
    }
}
