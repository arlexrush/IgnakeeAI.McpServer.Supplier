namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration
{
    /// <summary>
    /// Configuración del conector de inventario del ecommerce.
    ///
    /// SECCIÓN EN appsettings.json:
    ///   "EcommerceInventory": {
    ///     "Enabled": true,
    ///     "BaseUrl": "https://ecommerce.example.com",
    ///     "ApiKeyHeaderName": "X-Api-Key",
    ///     "ApiKeyValue": "",                   ← inyectar mediante variable de entorno / secret
    ///     "TimeoutSeconds": 10,
    ///     "ProductLookupPath": "/api/inventory/products/{productCode}",
    ///     "CatalogSyncPath": "/api/inventory/products",
    ///     "SyncPageSize": 100
    ///   }
    ///
    /// CAMPO MAPPING (ecommerce → CatalogProduct):
    ///   productCode       → ItemCode
    ///   productName       → Description (cuando description está vacío)
    ///   description       → Description
    ///   category          → Category
    ///   price             → UnitPrice
    ///   currency          → Currency
    ///   stock             → AvailableStock
    ///   unitToSell        → Unit
    ///   purchaseLeadTime  → LeadTimeDays  (normalizado a días según purchaseLeadTimeUnit)
    ///   status            → IsActive  (active → true, cualquier otro → false)
    /// </summary>
    public sealed class EcommerceInventoryOptions
    {
        public const string SectionName = "EcommerceInventory";

        /// <summary>Habilita o deshabilita la integración con el ecommerce.</summary>
        public bool Enabled { get; set; } = false;

        /// <summary>URL base de la API del ecommerce, sin barra final.</summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>Nombre del encabezado HTTP para la clave API.</summary>
        public string ApiKeyHeaderName { get; set; } = "X-Api-Key";

        /// <summary>
        /// Valor de la clave API. No confirmar credenciales reales;
        /// inyectar mediante variable de entorno EcommerceInventory__ApiKeyValue.
        /// </summary>
        public string ApiKeyValue { get; set; } = string.Empty;

        /// <summary>Tiempo máximo de espera por petición HTTP, en segundos.</summary>
        public int TimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Ruta de consulta individual de producto. El placeholder {productCode}
        /// será reemplazado por el código del producto.
        /// </summary>
        public string ProductLookupPath { get; set; } = "/api/inventory/products/{productCode}";

        /// <summary>Ruta del endpoint de catálogo para sincronización paginada.</summary>
        public string CatalogSyncPath { get; set; } = "/api/inventory/products";

        /// <summary>Tamaño de página en la sincronización del catálogo.</summary>
        public int SyncPageSize { get; set; } = 100;
    }
}
