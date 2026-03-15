namespace IgnakeeAI.McpServer.Supplier.Tests.Integration
{
    /// <summary>Fila de datos para generar un Excel de prueba.</summary>
    public record ExcelProductRow(
        string ItemCode,
        string Description,
        string Category,
        string Keywords,
        string Unit,
        decimal UnitPrice,
        string Currency,
        decimal? PackSize,
        decimal? PackPrice,
        string? Specification,
        string? Presentation,
        int? AvailableStock,
        int? LeadTimeDays,
        string? ProductUrl,
        bool IsOnSale,
        decimal? SalePrice,
        int? QualityRating);
}