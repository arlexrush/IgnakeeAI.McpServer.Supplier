namespace IgnakeeAI.McpServer.Supplier.Application.Models
{
    /// <summary>
    /// Resultado de consulta de disponibilidad.
    /// </summary>
    public record AvailabilityResult(bool Found, int AvailableStock = 0, int LeadTimeDays = 0);
}
