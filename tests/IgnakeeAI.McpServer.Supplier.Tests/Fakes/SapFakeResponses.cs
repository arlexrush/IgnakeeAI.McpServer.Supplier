using System.Text.Json;

namespace IgnakeeAI.McpServer.Supplier.Tests.Fakes
{
    /// <summary>
    /// Genera respuestas OData realistas que simulan el SAP Service Layer (Business One).
    /// Basadas en la estructura real de /b1s/v1/Items con paginación OData.
    /// </summary>
    public static class SapFakeResponses
    {
        // ── Login ────────────────────────────────────────────────────────────────

        /// <summary>Respuesta de Login exitosa en SAP Service Layer.</summary>
        public static string LoginSuccess() => JsonSerializer.Serialize(new
        {
            SessionId = "abc123-session-sap",
            Version = "1000200",
            SessionTimeout = 30
        });

        /// <summary>Respuesta de Login fallida (credenciales incorrectas).</summary>
        public static string LoginFailure() => JsonSerializer.Serialize(new
        {
            error = new
            {
                code = 301,
                message = new { lang = "en-us", value = "Invalid Login or password." }
            }
        });

        /// <summary>Confirmación de cierre de sesión SAP.</summary>
        public static string LogoutSuccess() => "{}";

        // ── Items OData ──────────────────────────────────────────────────────────

        /// <summary>
        /// Catálogo de materiales de construcción en formato OData SAP Business One.
        /// Incluye grupos de artículo como número (ItemsGroupCode) igual que la API real.
        /// </summary>
        public static string ItemsPage() => JsonSerializer.Serialize(new
        {
            odata_metadata = "https://sap-test:50000/b1s/v1/$metadata#Items",
            value = new object[]
            {
                new
                {
                    ItemCode = "SAP-CEM-001",
                    ItemName = "Cemento Portland SAP CEM II",
                    ItemsGroupCode = 10,
                    AvgStdPrice = 4.85,
                    SalesUnit = "KG",
                    QuantityOnStock = 10000.0,
                    SalesItem = "tYES"
                },
                new
                {
                    ItemCode = "SAP-ACE-002",
                    ItemName = "Acero corrugado SAP B500SD",
                    ItemsGroupCode = 20,
                    AvgStdPrice = 8.40,
                    SalesUnit = "MTR",
                    QuantityOnStock = 3500.0,
                    SalesItem = "tYES"
                },
                new
                {
                    ItemCode = "SAP-PIN-003",
                    ItemName = "Pintura plástica SAP blanca 15L",
                    ItemsGroupCode = 30,
                    AvgStdPrice = 42.90,
                    SalesUnit = "LTR",
                    QuantityOnStock = 820.0,
                    SalesItem = "tYES"
                }
            }
        });

        /// <summary>Catálogo vacío (sin productos).</summary>
        public static string ItemsEmpty() => JsonSerializer.Serialize(new
        {
            value = Array.Empty<object>()
        });

        /// <summary>
        /// Primera página OData con nextLink para probar la paginación.
        /// </summary>
        public static string ItemsPageWithNextLink(string baseUrl) => JsonSerializer.Serialize(new
        {
            value = new object[]
            {
                new
                {
                    ItemCode = "SAP-PAGE1-001",
                    ItemName = "Producto página 1",
                    ItemsGroupCode = 10,
                    AvgStdPrice = 5.00,
                    SalesUnit = "KG",
                    QuantityOnStock = 100.0,
                    SalesItem = "tYES"
                }
            },
            @_odata_nextLink = $"{baseUrl}/Items?$skiptoken=50"
        });

        /// <summary>Productos con campos opcionales en null/0 para probar defaults.</summary>
        public static string ItemsWithNullableFields() => JsonSerializer.Serialize(new
        {
            value = new object[]
            {
                new
                {
                    ItemCode = "SAP-NULL-001",
                    ItemName = "Producto SAP campos vacíos",
                    ItemsGroupCode = 0,        // grupo 0 → categoría "sap-group-0"
                    AvgStdPrice = 0.0,         // precio 0
                    SalesUnit = (string?)null, // unidad null → "ud"
                    QuantityOnStock = (double?)null,
                    SalesItem = "tYES"
                }
            }
        });
    }
}
