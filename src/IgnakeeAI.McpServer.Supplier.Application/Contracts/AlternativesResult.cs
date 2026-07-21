namespace IgnakeeAI.McpServer.Supplier.Application.Contracts;

public sealed class AlternativesResult
{
    public bool Found { get; set; }
    public int Count { get; set; }
    public List<AlternativeResultItem> Alternatives { get; set; } = new();
}

public sealed class AlternativeResultItem
{
    public string? ItemCode { get; set; }
    public string? Description { get; set; }
    public decimal? UnitPrice { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string? Currency { get; set; }
    public string? Unit { get; set; }
    public decimal? PackSize { get; set; }
    public decimal? PackPrice { get; set; }
    public string? Specification { get; set; }
    public string? Presentation { get; set; }
    public decimal? QualityRating { get; set; }
    public bool IsOnSale { get; set; }
    public decimal? AvailableStock { get; set; }
    public int? LeadTimeDays { get; set; }
    public string? Url { get; set; }
    public string? Reason { get; set; }
}
