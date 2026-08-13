using IgnakeeAI.McpServer.Supplier.Domain.Entities;

namespace IgnakeeAI.McpServer.Supplier.Application.Contracts
{
    /// <summary>Resultado de una página del catálogo ecommerce con su metadata de paginación.</summary>
    public sealed record EcommerceCatalogPage(
        IReadOnlyList<CatalogProduct> Products,
        int PageIndex,
        int PageCount);
}
