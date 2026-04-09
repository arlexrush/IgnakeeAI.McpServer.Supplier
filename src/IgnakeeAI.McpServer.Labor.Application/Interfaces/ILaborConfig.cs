namespace IgnakeeAI.McpServer.Labor.Application.Interfaces
{
    /// <summary>
    /// Configuración del proveedor de mano de obra.
    /// Se inyecta en las tools MCP para devolver datos de contacto de la empresa.
    /// </summary>
    public interface ILaborConfig
    {
        public string AgencyName { get; }
        public string ContactEmail { get; }
        public string ContactPhone { get; }
        public string ContactAddress { get; }
        public string BusinessHours { get; }
    }
}
