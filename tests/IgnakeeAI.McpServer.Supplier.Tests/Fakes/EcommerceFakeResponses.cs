using System.Text.Json;

namespace IgnakeeAI.McpServer.Supplier.Tests.Fakes
{
    /// <summary>
    /// Genera respuestas JSON simuladas para la API de inventario del ecommerce.
    /// Refleja el contrato del endpoint REST de inventario:
    ///   GET /api/inventory/products/{productCode}  → EcommerceProductDto
    ///   GET /api/inventory/products?...            → EcommerceCatalogPageDto
    /// </summary>
    public static class EcommerceFakeResponses
    {
        // ── Producto individual ──────────────────────────────────────────────────

        public static string ProductSingle(
            string productCode = "ECO-001",
            string productName = "Cemento Portland Ecommerce",
            string description = "CEM II/B-L 32.5R — saco 25 kg",
            string category = "cementos",
            decimal price = 5.20m,
            string currency = "EUR",
            int stock = 500,
            string unitToSell = "saco",
            int purchaseLeadTime = 3,
            string purchaseLeadTimeUnit = "days",
            string status = "active") =>
            JsonSerializer.Serialize(new
            {
                productCode,
                productId = "pid-001",
                productName,
                description,
                category,
                price,
                currency,
                stock,
                unitToSell,
                purchaseLeadTime,
                purchaseLeadTimeUnit,
                status
            });

        public static string ProductWithEmptyCode() =>
            JsonSerializer.Serialize(new
            {
                productCode = "",
                productName = "Producto sin código",
                price = 1.00,
                status = "active"
            });

        public static string ProductMalformedJson() => "{ this is not valid JSON ,,, }";

        // ── Catálogo paginado ────────────────────────────────────────────────────

        /// <summary>Página de catálogo con N productos activos.</summary>
        public static string CatalogPage(int page, int pageSize, bool hasNextPage, int totalItems = 150) =>
            JsonSerializer.Serialize(new
            {
                items = Enumerable.Range(1, pageSize).Select(i => new
                {
                    productCode = $"ECO-P{page}-{i:D3}",
                    productName = $"Producto página {page} ítem {i}",
                    description = $"Descripción del producto {i} en página {page}",
                    category = "materiales",
                    price = 10.00 + i,
                    currency = "EUR",
                    stock = 100 * i,
                    unitToSell = "ud",
                    purchaseLeadTime = 2,
                    purchaseLeadTimeUnit = "days",
                    status = "active"
                }).ToArray(),
                totalItems,
                page,
                pageSize,
                hasNextPage
            });

        /// <summary>Página vacía (sin productos).</summary>
        public static string CatalogPageEmpty() =>
            JsonSerializer.Serialize(new
            {
                items = Array.Empty<object>(),
                totalItems = 0,
                page = 1,
                pageSize = 100,
                hasNextPage = false
            });

        /// <summary>Página con un producto cuyo ProductCode está vacío (debe ser ignorado).</summary>
        public static string CatalogPageWithEmptyCode() =>
            JsonSerializer.Serialize(new
            {
                items = new object[]
                {
                    new { productCode = "", productName = "Sin código", price = 1.0, status = "active" },
                    new
                    {
                        productCode = "ECO-VALID-001",
                        productName = "Producto válido",
                        price = 5.0,
                        currency = "EUR",
                        stock = 10,
                        unitToSell = "ud",
                        status = "active"
                    }
                },
                totalItems = 2,
                page = 1,
                pageSize = 100,
                hasNextPage = false
            });

        /// <summary>Respuesta JSON con estructura incorrecta (objeto en lugar de lista de items).</summary>
        public static string CatalogPageMalformedJson() => "not-json-at-all";

        // ── Lead time en horas y semanas ─────────────────────────────────────────

        public static string ProductWithLeadTimeInHours(string productCode = "ECO-H-001") =>
            JsonSerializer.Serialize(new
            {
                productCode,
                productName = "Producto lead time en horas",
                price = 3.50,
                currency = "EUR",
                stock = 200,
                unitToSell = "ud",
                purchaseLeadTime = 48,      // 48 horas → 2 días
                purchaseLeadTimeUnit = "hours",
                status = "active"
            });

        public static string ProductWithLeadTimeInWeeks(string productCode = "ECO-W-001") =>
            JsonSerializer.Serialize(new
            {
                productCode,
                productName = "Producto lead time en semanas",
                price = 7.00,
                currency = "EUR",
                stock = 50,
                unitToSell = "ud",
                purchaseLeadTime = 2,       // 2 semanas → 14 días
                purchaseLeadTimeUnit = "weeks",
                status = "active"
            });
    }
}
