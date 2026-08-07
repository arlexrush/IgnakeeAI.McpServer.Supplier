using IgnakeeAI.McpServer.Supplier.Application.Contracts;
using IgnakeeAI.McpServer.Supplier.Domain.Entities;

namespace IgnakeeAI.McpServer.Supplier.Application.Interfaces;

public interface IEcommerceInventoryService
{
    bool IsEnabled { get; }

    Task<CatalogProduct?> FindByCodeAsync(string itemCode, CancellationToken ct = default);

    Task<EcommerceInventorySyncResult> SyncCatalogAsync(CancellationToken ct = default);
}
