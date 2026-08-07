namespace IgnakeeAI.McpServer.Supplier.Application.Contracts;

public static class CatalogSyncAuditSources
{
    public const string Erp = "erp";
    public const string Csv = "csv";
    public const string Excel = "excel";
    public const string Ecommerce = "ecommerce";
    public const string Manual = "manual";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Erp, Csv, Excel, Ecommerce, Manual
    };
}
