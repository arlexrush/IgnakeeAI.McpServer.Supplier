namespace IgnakeeAI.McpServer.Supplier.Application.Contracts;

public sealed class AvailabilityResult
{
    public bool Found { get; set; }
    public string? ItemCode { get; set; }
    public decimal? AvailableStock { get; set; }
    public int? LeadTimeDays { get; set; }
    public string? Message { get; set; }
}
