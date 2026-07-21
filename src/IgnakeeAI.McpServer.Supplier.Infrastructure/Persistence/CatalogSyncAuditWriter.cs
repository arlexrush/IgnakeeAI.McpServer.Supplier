using IgnakeeAI.McpServer.Supplier.Application.Contracts;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;

public sealed class CatalogSyncAuditWriter
{
    private readonly SupplierCatalogDbContext _db;

    public CatalogSyncAuditWriter(SupplierCatalogDbContext db) => _db = db;

    public async Task<CatalogSyncAudit> WriteAsync(
        string source,
        string? erpProvider,
        int productsRead,
        int productsCreated,
        int productsUpdated,
        int productsRejected,
        DateTimeOffset startedAt,
        bool succeeded,
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        var completedAt = DateTimeOffset.UtcNow;
        var audit = new CatalogSyncAuditEntity
        {
            SyncId = Guid.NewGuid(),
            Source = source,
            ErpProvider = erpProvider,
            ProductsRead = productsRead,
            ProductsCreated = productsCreated,
            ProductsUpdated = productsUpdated,
            ProductsRejected = productsRejected,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            Succeeded = succeeded,
            Error = error
        };

        _db.SyncAudits.Add(audit);
        await _db.SaveChangesAsync(cancellationToken);

        return new CatalogSyncAudit(
            audit.SyncId,
            audit.Source,
            audit.ErpProvider,
            audit.ProductsRead,
            audit.ProductsCreated,
            audit.ProductsUpdated,
            audit.ProductsRejected,
            audit.StartedAt,
            audit.CompletedAt,
            audit.Succeeded,
            audit.Error);
    }
}
