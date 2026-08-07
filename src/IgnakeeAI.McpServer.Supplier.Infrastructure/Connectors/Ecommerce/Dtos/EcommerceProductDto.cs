using System.Text.Json.Serialization;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Ecommerce.Dtos
{
    /// <summary>
    /// DTO para un producto individual devuelto por la API de inventario del ecommerce.
    /// Refleja el contrato del endpoint GET /api/v1/inventory/{productCode}.
    ///
    /// MAPPING → CatalogProduct:
    ///   productCode          → ItemCode
    ///   productId            → referencia numérica (int?)
    ///   productName/desc     → Description
    ///   category             → Category
    ///   price                → UnitPrice  (decimal? — null tolerado)
    ///   currency             → Currency (default: "EUR")
    ///   isAvailableForSale   → contribuye a IsActive
    ///   stock                → AvailableStock
    ///   unitToSell           → Unit
    ///   purchaseLeadTime     → LeadTimeDays (normalizado a días)
    ///   status "Active"      → contribuye a IsActive
    /// </summary>
    public sealed class EcommerceProductDto
    {
        [JsonPropertyName("productCode")]
        public string? ProductCode { get; set; }

        [JsonPropertyName("productId")]
        public int? ProductId { get; set; }

        [JsonPropertyName("productName")]
        public string? ProductName { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("category")]
        public string? Category { get; set; }

        [JsonPropertyName("price")]
        public decimal? Price { get; set; }

        [JsonPropertyName("currency")]
        public string? Currency { get; set; }

        [JsonPropertyName("isAvailableForSale")]
        public bool IsAvailableForSale { get; set; }

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
    /// DTO de página del catálogo paginado devuelto por GET /api/v1/inventory.
    /// Refleja la semántica PaginationVm&lt;T&gt; del ecommerce.
    /// </summary>
    public sealed class EcommerceCatalogPageDto
    {
        [JsonPropertyName("data")]
        public List<EcommerceProductDto> Data { get; set; } = [];

        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("pageIndex")]
        public int PageIndex { get; set; }

        [JsonPropertyName("pageSize")]
        public int PageSize { get; set; }

        [JsonPropertyName("pageCount")]
        public int PageCount { get; set; }

        [JsonPropertyName("resultByPage")]
        public int ResultByPage { get; set; }
    }
}
