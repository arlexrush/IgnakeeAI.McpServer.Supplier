namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration;

public sealed class EcommerceInventoryOptions
{
    public const string SectionName = "EcommerceInventory";

    public bool Enabled { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string AuthenticationHeaderName { get; set; } = "X-Api-Key";
    public string AuthenticationHeaderValue { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 15;
    public string ProductLookupPathTemplate { get; set; } = "/api/inventory/v1/products/{productCode}";
    public string CatalogSyncPathTemplate { get; set; } = "/api/inventory/v1/products/active?page={page}&pageSize={pageSize}";
    public int CatalogSyncPageSize { get; set; } = 100;
}
