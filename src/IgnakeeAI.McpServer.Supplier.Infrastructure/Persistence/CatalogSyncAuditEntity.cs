namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;

public sealed class CatalogSyncAuditEntity
{
    public Guid SyncId { get; set; }
    public string Source { get; set; } = string.Empty;
    public string? ErpProvider { get; set; }
    public int ProductsRead { get; set; }
    public int ProductsCreated { get; set; }
    public int ProductsUpdated { get; set; }
    public int ProductsRejected { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public bool Succeeded { get; set; }
    public string? Error { get; set; }
}
