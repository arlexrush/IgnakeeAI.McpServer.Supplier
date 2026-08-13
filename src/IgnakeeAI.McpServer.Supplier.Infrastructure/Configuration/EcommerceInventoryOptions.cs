namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration
{
    /// <summary>
    /// Configuración del conector de inventario del ecommerce.
    ///
    /// SECCIÓN EN appsettings.json:
    ///   "EcommerceInventory": {
    ///     "Enabled": true,
    ///     "BaseUrl": "https://ecommerce.example.com",
    ///     "BearerToken": "",               ← inyectar mediante variable de entorno EcommerceInventory__BearerToken
    ///     "TimeoutSeconds": 10,
    ///     "ProductLookupPath": "/api/v1/inventory/{productCode}",
    ///     "CatalogSyncPath": "/api/v1/inventory",
    ///     "SyncPageSize": 50
    ///   }
    ///
    /// AUTENTICACIÓN:
    ///   La identidad técnica debe tener el rol INVENTORY_READER (o ADMIN para break-glass).
    ///   El token se envía en el encabezado: Authorization: ******;token&gt;
    ///   No confirmar el valor en el repositorio; inyectarlo desde variable de entorno o secret manager.
    ///
    /// CAMPO MAPPING (ecommerce → CatalogProduct):
    ///   productCode          → ItemCode
    ///   productId            → (almacenado como referencia; int? en el DTO)
    ///   productName          → Description (cuando description está vacío)
    ///   description          → Description
    ///   category             → Category
    ///   price                → UnitPrice  (decimal? — null tolerado; se guarda como null/0)
    ///   currency             → Currency
    ///   isAvailableForSale   → IsActive  (conjuntamente con status)
    ///   stock                → AvailableStock
    ///   unitToSell           → Unit
    ///   purchaseLeadTime     → LeadTimeDays  (normalizado a días según purchaseLeadTimeUnit)
    ///   status "Active"      → IsActive = true  (junto con isAvailableForSale)
    ///
    /// PAGINACIÓN (PaginationVm):
    ///   Campos de respuesta: data[], count, pageIndex, pageSize, pageCount, resultByPage
    /// </summary>
    public sealed class EcommerceInventoryOptions
    {
        public const string SectionName = "EcommerceInventory";

        /// <summary>Habilita o deshabilita la integración con el ecommerce.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>URL base de la API del ecommerce, sin barra final.</summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// Token Bearer para autenticación service-to-service.
        /// La identidad técnica debe tener el rol INVENTORY_READER.
        /// No confirmar en el repositorio; inyectar mediante EcommerceInventory__BearerToken.
        /// </summary>
        public string BearerToken { get; set; } = string.Empty;

        /// <summary>Tiempo máximo de espera por petición HTTP, en segundos.</summary>
        public int TimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Ruta de consulta individual de producto. El placeholder {productCode}
        /// será reemplazado por el código del producto.
        /// </summary>
        public string ProductLookupPath { get; set; } = "/api/v1/inventory/{productCode}";

        /// <summary>Ruta del endpoint de catálogo para sincronización paginada.</summary>
        public string CatalogSyncPath { get; set; } = "/api/v1/inventory";

        /// <summary>
        /// Tamaño de página en la sincronización del catálogo.
        /// El ecommerce aplica un máximo efectivo de 50 (PaginationBaseQuery.MaxPagesSize).
        /// La sincronización recorta los valores mayores a ese límite y determina el
        /// final de la iteración mediante pageIndex y pageCount del envelope.
        /// </summary>
        public int SyncPageSize { get; set; } = 50;
    }
}
