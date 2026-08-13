using System.Text.Json;

namespace IgnakeeAI.McpServer.Supplier.Tests.Fakes
{
    /// <summary>
    /// Genera respuestas JSON simuladas para la API de inventario del ecommerce.
    /// Refleja el contrato real del endpoint REST de inventario:
    ///   GET /api/v1/inventory/{productCode}  → EcommerceProductDto
    ///   GET /api/v1/inventory?pageIndex=n&amp;pageSize=n  → PaginationVm (data[], count, pageIndex, pageSize, pageCount, resultByPage)
    /// </summary>
    public static class EcommerceFakeResponses
    {
        // ── Producto individual ──────────────────────────────────────────────────

        public static string ProductSingle(
            string productCode = "ECO-001",
            string productName = "Cemento Portland Ecommerce",
            string description = "CEM II/B-L 32.5R — saco 25 kg",
            string category = "cementos",
            decimal? price = 5.20m,
            string currency = "EUR",
            bool isAvailableForSale = true,
            int stock = 500,
            string unitToSell = "saco",
            int purchaseLeadTime = 3,
            string purchaseLeadTimeUnit = "days",
            string status = "Active") =>
            JsonSerializer.Serialize(new
            {
                productCode,
                productId = 1001,
                productName,
                description,
                category,
                price,
                currency,
                isAvailableForSale,
                stock,
                unitToSell,
                purchaseLeadTime,
                purchaseLeadTimeUnit,
                status
            });

        public static string ProductWithNullPrice(string productCode = "ECO-NOPRICE") =>
            JsonSerializer.Serialize(new
            {
                productCode,
                productId = 1002,
                productName = "Producto sin precio",
                description = "Precio pendiente de definir",
                category = "otros",
                price = (decimal?)null,
                currency = "EUR",
                isAvailableForSale = true,
                stock = 10,
                unitToSell = "ud",
                purchaseLeadTime = 1,
                purchaseLeadTimeUnit = "days",
                status = "Active"
            });

        public static string ProductWithEmptyCode() =>
            JsonSerializer.Serialize(new
            {
                productCode = "",
                productId = 0,
                productName = "Producto sin código",
                price = (decimal?)1.00m,
                isAvailableForSale = true,
                status = "Active"
            });

        public static string ProductMalformedJson() => "{ this is not valid JSON ,,, }";

        // ── Catálogo paginado (PaginationVm) ─────────────────────────────────────

        /// <summary>Página de catálogo con N productos activos usando contrato PaginationVm del ecommerce.</summary>
        public static string CatalogPage(int pageIndex, int pageSize, int totalCount = 150, int? reportedPageSize = null) =>
            JsonSerializer.Serialize(new
            {
                data = Enumerable.Range(1, pageSize).Select(i => new
                {
                    productCode = $"ECO-P{pageIndex}-{i:D3}",
                    productId = pageIndex * 1000 + i,
                    productName = $"Producto página {pageIndex} ítem {i}",
                    description = $"Descripción del producto {i} en página {pageIndex}",
                    category = "materiales",
                    price = (decimal?)(10.00m + i),
                    currency = "EUR",
                    isAvailableForSale = true,
                    stock = (int?)(100 * i),
                    unitToSell = "ud",
                    purchaseLeadTime = (int?)2,
                    purchaseLeadTimeUnit = "days",
                    status = "Active"
                }).ToArray(),
                count = totalCount,
                pageIndex,
                pageSize = reportedPageSize ?? pageSize,
                pageCount = (int)Math.Ceiling((double)totalCount / (reportedPageSize ?? pageSize)),
                resultByPage = pageSize
            });

        /// <summary>Página vacía (sin productos).</summary>
        public static string CatalogPageEmpty() =>
            JsonSerializer.Serialize(new
            {
                data = Array.Empty<object>(),
                count = 0,
                pageIndex = 1,
                pageSize = 100,
                pageCount = 0,
                resultByPage = 100
            });

        /// <summary>Página con un producto cuyo ProductCode está vacío (debe ser ignorado).</summary>
        public static string CatalogPageWithEmptyCode() =>
            JsonSerializer.Serialize(new
            {
                data = new object[]
                {
                    new { productCode = "", productId = 0, productName = "Sin código", price = (decimal?)1.0m, isAvailableForSale = true, status = "Active" },
                    new
                    {
                        productCode = "ECO-VALID-001",
                        productId = 999,
                        productName = "Producto válido",
                        price = (decimal?)5.0m,
                        currency = "EUR",
                        isAvailableForSale = true,
                        stock = (int?)10,
                        unitToSell = "ud",
                        status = "Active"
                    }
                },
                count = 2,
                pageIndex = 1,
                pageSize = 100,
                pageCount = 1,
                resultByPage = 100
            });

        /// <summary>Respuesta JSON con estructura incorrecta.</summary>
        public static string CatalogPageMalformedJson() => "not-json-at-all";

        /// <summary>Página con un producto cuyo isAvailableForSale = false (debe ser importado pero inactivo).</summary>
        public static string CatalogPageWithUnavailableProduct() =>
            JsonSerializer.Serialize(new
            {
                data = new object[]
                {
                    new
                    {
                        productCode = "ECO-UNAVAIL-001",
                        productId = 4001,
                        productName = "Producto no disponible para venta",
                        price = (decimal?)9.99m,
                        currency = "EUR",
                        isAvailableForSale = false,
                        stock = (int?)0,
                        unitToSell = "ud",
                        status = "Active"
                    }
                },
                count = 1,
                pageIndex = 1,
                pageSize = 10,
                pageCount = 1,
                resultByPage = 10
            });

        // ── Lead time en horas y semanas ─────────────────────────────────────────

        public static string ProductWithLeadTimeInHours(string productCode = "ECO-H-001") =>
            JsonSerializer.Serialize(new
            {
                productCode,
                productId = 2001,
                productName = "Producto lead time en horas",
                price = (decimal?)3.50m,
                currency = "EUR",
                isAvailableForSale = true,
                stock = (int?)200,
                unitToSell = "ud",
                purchaseLeadTime = (int?)48,      // 48 horas → 2 días
                purchaseLeadTimeUnit = "hours",
                status = "Active"
            });

        public static string ProductWithLeadTimeInWeeks(string productCode = "ECO-W-001") =>
            JsonSerializer.Serialize(new
            {
                productCode,
                productId = 2002,
                productName = "Producto lead time en semanas",
                price = (decimal?)7.00m,
                currency = "EUR",
                isAvailableForSale = true,
                stock = (int?)50,
                unitToSell = "ud",
                purchaseLeadTime = (int?)2,       // 2 semanas → 14 días
                purchaseLeadTimeUnit = "weeks",
                status = "Active"
            });

        // ── Productos inactivos / no disponibles ──────────────────────────────────

        public static string ProductUnavailable(string productCode = "ECO-UNAVAIL") =>
            JsonSerializer.Serialize(new
            {
                productCode,
                productId = 3001,
                productName = "Producto no disponible",
                price = (decimal?)9.99m,
                currency = "EUR",
                isAvailableForSale = false,
                stock = (int?)0,
                unitToSell = "ud",
                purchaseLeadTime = (int?)0,
                purchaseLeadTimeUnit = "days",
                status = "Active"
            });

        public static string ProductDiscontinued(string productCode = "ECO-DISC") =>
            JsonSerializer.Serialize(new
            {
                productCode,
                productId = 3002,
                productName = "Producto discontinuado",
                price = (decimal?)5.00m,
                currency = "EUR",
                isAvailableForSale = true,
                stock = (int?)0,
                unitToSell = "ud",
                purchaseLeadTime = (int?)0,
                purchaseLeadTimeUnit = "days",
                status = "Discontinued"
            });
    }
}
