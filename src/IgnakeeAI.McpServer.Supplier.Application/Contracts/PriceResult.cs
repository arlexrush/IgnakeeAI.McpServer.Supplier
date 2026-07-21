namespace IgnakeeAI.McpServer.Supplier.Application.Contracts;

public sealed class PriceResult
{
    public bool Found { get; set; }
    public string? ItemCode { get; set; }
    public string? Description { get; set; }
    public decimal? UnitPrice { get; set; }
    public bool IsOnSale { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string? Currency { get; set; }
    public string? Unit { get; set; }
    public decimal? PackSize { get; set; }
    public decimal? PackPrice { get; set; }
    public DateTimeOffset? ValidUntil { get; set; }
    public string? VendorName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactAddress { get; set; }
}
