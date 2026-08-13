using IgnakeeAI.McpServer.Supplier.Application.Contracts;
using IgnakeeAI.McpServer.Supplier.Application.Interfaces;
using IgnakeeAI.McpServer.Supplier.Domain.Entities;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Ecommerce
{
    /// <summary>
    /// Implementación nula del cliente de inventario ecommerce.
    /// Se registra cuando EcommerceInventory:Enabled = false.
    /// Garantiza que CatalogSearchService pueda recibir el puerto sin fallos de inyección.
    /// </summary>
    internal sealed class DisabledEcommerceInventoryClient : IEcommerceInventoryClient
    {
        public bool IsEnabled => false;

        public Task<CatalogProduct?> GetProductByCodeAsync(string productCode, CancellationToken ct = default)
            => Task.FromResult<CatalogProduct?>(null);

        public Task<EcommerceCatalogPage> GetCatalogPageAsync(int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult(new EcommerceCatalogPage([], page, 0));
    }
}
