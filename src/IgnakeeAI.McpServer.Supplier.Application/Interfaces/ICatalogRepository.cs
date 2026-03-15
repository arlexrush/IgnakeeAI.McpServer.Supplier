using IgnakeeAI.McpServer.Supplier.Domain.Entities;

namespace IgnakeeAI.McpServer.Supplier.Application.Interfaces
{
    /// <summary>
    /// Puerto de acceso al catálogo del proveedor.
    /// Infrastructure implementa este contrato con EF Core, Excel, CSV o un ERP.
    /// Las tools MCP consumen este servicio sin conocer la fuente de datos.
    /// </summary>
    public interface ICatalogRepository
    {
        /// <summary>Busca un producto por código (SKU) exacto.</summary>
        Task<CatalogProduct?> FindByCodeAsync(string itemCode, CancellationToken ct = default);

        /// <summary>Busca productos por términos en descripción y keywords.</summary>
        Task<CatalogProduct?> FindByDescriptionAsync(
            IReadOnlyList<string> searchTerms, CancellationToken ct = default);

        /// <summary>Infiere la categoría de un producto a partir de su descripción.</summary>
        Task<string?> InferCategoryAsync(
            IReadOnlyList<string> searchTerms, CancellationToken ct = default);

        /// <summary>Busca productos más baratos en la categoría.</summary>
        Task<IReadOnlyList<CatalogProduct>> FindCheaperInCategoryAsync(
            string category, decimal referencePrice, int max, CancellationToken ct = default);

        /// <summary>Busca productos de alta calidad en la categoría.</summary>
        Task<IReadOnlyList<CatalogProduct>> FindBetterQualityAsync(
            string category, int minRating, int max, CancellationToken ct = default);

        /// <summary>Busca productos en oferta en la categoría.</summary>
        Task<IReadOnlyList<CatalogProduct>> FindOnSaleAsync(
            string category, int max, CancellationToken ct = default);

        /// <summary>Busca productos con pack para cálculo de presentación óptima.</summary>
        Task<IReadOnlyList<CatalogProduct>> FindWithPackInfoAsync(
            string category, CancellationToken ct = default);
    }
}
