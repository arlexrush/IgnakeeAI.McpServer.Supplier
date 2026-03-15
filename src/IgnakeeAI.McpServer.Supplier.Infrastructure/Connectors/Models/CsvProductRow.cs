namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Models
{
    /// <summary>Mapeo CSV → propiedades planas.</summary>
    public class CsvProductRow
    {
        public string ItemCode { get; set; } = "";
        public string? Description { get; set; }
        public string? Category { get; set; }
        public string? Keywords { get; set; }
        public string? Unit { get; set; }
        public decimal UnitPrice { get; set; }
        public string? Currency { get; set; }
        public decimal? PackSize { get; set; }
        public decimal? PackPrice { get; set; }
        public string? Specification { get; set; }
        public string? Presentation { get; set; }
        public int? AvailableStock { get; set; }
        public int? LeadTimeDays { get; set; }
        public string? ProductUrl { get; set; }
        public bool IsOnSale { get; set; }
        public decimal? SalePrice { get; set; }
        public int? QualityRating { get; set; }
    }
}
