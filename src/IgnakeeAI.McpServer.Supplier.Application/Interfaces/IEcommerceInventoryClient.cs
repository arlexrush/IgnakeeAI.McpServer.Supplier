using IgnakeeAI.McpServer.Supplier.Domain.Entities;

namespace IgnakeeAI.McpServer.Supplier.Application.Interfaces
{
    /// <summary>
    /// Puerto de acceso al inventario del ecommerce.
    /// Infrastructure implementa este contrato con el conector HTTP autenticado.
    /// CatalogSearchService lo consume para consultas en tiempo real de disponibilidad.
    /// </summary>
    public interface IEcommerceInventoryClient
    {
        /// <summary>Indica si la integración con el ecommerce está habilitada.</summary>
        bool IsEnabled { get; }

        /// <summary>
        /// Consulta un producto directamente al ecommerce por su código de producto (ProductCode).
        /// Devuelve null si no se encuentra (404). Lanza excepción ante fallos técnicos.
        /// </summary>
        Task<CatalogProduct?> GetProductByCodeAsync(string productCode, CancellationToken ct = default);

        /// <summary>
        /// Obtiene una página del catálogo activo del ecommerce para sincronización.
        /// Devuelve lista vacía si la página está fuera de rango.
        /// </summary>
        Task<IReadOnlyList<CatalogProduct>> GetCatalogPageAsync(int page, int pageSize, CancellationToken ct = default);
    }
}
