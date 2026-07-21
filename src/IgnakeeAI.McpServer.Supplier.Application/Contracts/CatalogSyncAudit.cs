namespace IgnakeeAI.McpServer.Supplier.Application.Contracts;

public sealed record CatalogSyncAudit(
    Guid SyncId,
    string Source,
    string? ErpProvider,
    int ProductsRead,
    int ProductsCreated,
    int ProductsUpdated,
    int ProductsRejected,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    bool Succeeded,
    string? Error);
