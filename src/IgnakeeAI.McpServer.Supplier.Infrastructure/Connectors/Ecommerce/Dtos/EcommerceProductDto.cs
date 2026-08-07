using System.Text.Json.Serialization;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Ecommerce.Dtos
{
    /// <summary>
    /// DTO para un producto individual devuelto por la API de inventario del ecommerce.
    /// Refleja el contrato del endpoint GET /api/inventory/products/{productCode}.
    ///
    /// MAPPING → CatalogProduct:
    ///   productCode       → ItemCode
    ///   productName/desc  → Description
    ///   category          → Category
    ///   price             → UnitPrice
    ///   currency          → Currency (default: "EUR")
    ///   stock             → AvailableStock
    ///   unitToSell        → Unit
    ///   purchaseLeadTime  → LeadTimeDays (normalizado a días)
    ///   status "active"   → IsActive = true
    /// </summary>
    public sealed class EcommerceProductDto
    {
        [JsonPropertyName("productCode")]
        public string? ProductCode { get; set; }

        [JsonPropertyName("productId")]
        public string? ProductId { get; set; }

        [JsonPropertyName("productName")]
        public string? ProductName { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("price")]
        public decimal Price { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("stock")]
        public int? Stock { get; set; }

        [JsonPropertyName("unitToSell")]
        public string? UnitToSell { get; set; }

        [JsonPropertyName("purchaseLeadTime")]
        public int? PurchaseLeadTime { get; set; }

        [JsonPropertyName("purchaseLeadTimeUnit")]
        public string? PurchaseLeadTimeUnit { get; set; }

        [JsonPropertyName("status")]
        public string? Status { get; set; }
    }

    /// <summary>
    /// DTO de página del catálogo paginado devuelto por GET /api/inventory/products.
    /// </summary>
    public sealed class EcommerceCatalogPageDto
    {
        [JsonPropertyName("items")]
        public List<EcommerceProductDto> Items { get; set; } = [];

        [JsonPropertyName("totalItems")]
        public int TotalItems { get; set; }

        [JsonPropertyName("page")]
        public int Page { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("hasNextPage")]
        public bool HasNextPage { get; set; }
    }
}
