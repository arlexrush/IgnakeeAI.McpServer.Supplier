namespace IgnakeeAI.McpServer.Supplier.Application.Contracts;

public sealed class BusinessHoursResult
{
    public string? Hours { get; set; }
    public string? VendorName { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactAddress { get; set; }
}
