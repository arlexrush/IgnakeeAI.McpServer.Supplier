namespace IgnakeeAI.McpServer.Supplier.Application.Contracts;

/// <summary>Names of the public MCP tools. These values are part of the Legio contract.</summary>
public static class SupplierMcpToolNames
{
    public const string GetPrice = "GetPrice";
    public const string SearchAlternatives = "SearchAlternatives";
    public const string CheckAvailability = "CheckAvailability";
    public const string GetBusinessHours = "GetBusinessHours";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        GetPrice,
        SearchAlternatives,
        CheckAvailability,
        GetBusinessHours
    };
}
