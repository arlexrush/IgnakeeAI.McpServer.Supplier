namespace IgnakeeAI.McpServer.Supplier.Application.Models
{
    /// <summary>
    /// Resultado de la búsqueda de precio. Se serializa como JSON
    /// y se devuelve al cliente MCP (Aristóteles).
    /// </summary>
    public record PriceResult(
    bool Found,
    string? ItemCode = null,
    string? Description = null,
    decimal UnitPrice = 0,
    string Currency = "EUR",
    string? Unit = null,
    decimal? PackSize = null,
    decimal? PackPrice = null,
    string? Specification = null,
    string? Presentation = null,
    bool IsOnSale = false,
    decimal? OriginalPrice = null,
    int? QualityRating = null,
    string? Url = null,
    DateTime? ValidUntil = null,
    DateTime? UpdatedAt = null,
    string? ContactEmail = null,
    string? ContactPhone = null,
    string? ContactAddress = null);
}
