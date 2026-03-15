namespace IgnakeeAI.McpServer.Supplier.Application.Models
{
    /// <summary>
    /// Resultado de búsqueda de alternativas serializado para el cliente MCP.
    /// </summary>
    public record AlternativeResult(
        string ItemCode,
        string Description,
        decimal UnitPrice,
        decimal? OriginalPrice,
        string Currency,
        string? Unit,
        decimal? PackSize,
        decimal? PackPrice,
        string? Specification,
        string? Presentation,
        int? QualityRating,
        bool IsOnSale,
        int? AvailableStock,
        int? LeadTimeDays,
        string? Url,
        string Reason);
}
