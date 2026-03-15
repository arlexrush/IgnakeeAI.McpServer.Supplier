using IgnakeeAI.McpServer.Supplier.Domain.Entities;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Erp
{
    /// <summary>
    /// Contrato para conectores ERP. Cada ERP implementa este puerto.
    /// El conector lee productos del ERP y los devuelve como CatalogProduct.
    ///
    /// NOTA: Los conectores ERP son OPCIONALES. Si el proveedor no usa ERP,
    /// puede alimentar el catálogo desde Excel/CSV o directamente en la BD.
    /// </summary>
    public interface IErpConnector
    {
        /// <summary>Nombre del ERP (ej. "Odoo", "SAP", "Holded").</summary>
        string ErpName { get; }

        /// <summary>¿Está configurado y disponible?</summary>
        Task<bool> IsAvailableAsync(CancellationToken ct = default);

        /// <summary>Sincroniza productos del ERP con el catálogo local.</summary>
        Task<int> SyncProductsAsync(CancellationToken ct = default);

        /// <summary>Busca un producto directamente en el ERP por código.</summary>
        Task<CatalogProduct?> FindProductAsync(string itemCode, CancellationToken ct = default);
    }
}
