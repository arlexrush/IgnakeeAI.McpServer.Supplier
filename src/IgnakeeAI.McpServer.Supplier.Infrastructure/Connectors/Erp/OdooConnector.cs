using IgnakeeAI.McpServer.Supplier.Domain.Entities;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text.Json;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Erp
{
    /// <summary>
    /// Conector para Odoo ERP (v14+).
    ///
    /// Odoo expone una API JSON-RPC en /jsonrpc. Este conector:
    ///   1. Se autentica con usuario/password para obtener session_id.
    ///   2. Llama a product.product/search_read para leer el catálogo.
    ///   3. Mapea los campos de Odoo a CatalogProduct.
    ///   4. Hace upsert en la BD local del servidor MCP.
    ///
    /// PERSONALIZACIÓN:
    ///   - Si tu Odoo tiene campos personalizados (x_quality_rating, x_pack_size, etc.),
    ///     añádelos al array "fields" en el paso 2 y al mapeo en el paso 3.
    ///   - Si usas el módulo "product.packaging", puedes leer los packs desde ahí.
    /// 
    /// CONFIGURACIÓN (appsettings.json):
    ///   "Erp": {
    ///     "Provider": "Odoo",
    ///     "Odoo": {
    ///       "Url": "https://mi-odoo.com",
    ///       "Database": "mi_empresa",
    ///       "Username": "api_user",
    ///       "Password": "api_password"
    ///     }
    ///   }
    /// </summary>
    public class OdooConnector : IErpConnector
    {
        private readonly HttpClient _http;
        private readonly SupplierCatalogDbContext _db;
        private readonly DataSourceSettings _config;
        private readonly ILogger<OdooConnector> _logger;

        public string ErpName => "Odoo";

        // ── Campos de Odoo a leer ────────────────────────────────────────────────
        // PERSONALIZA AQUÍ: añade tus campos x_ personalizados al array.
        private static readonly string[] OdooFields =
        [
            "default_code",       // → ItemCode
            "name",               // → Description
            "categ_id",           // → Category (array [id, nombre])
            "list_price",         // → UnitPrice
            "uom_id",             // → Unit (array [id, nombre])
            "qty_available",      // → AvailableStock
            "description_sale",   // → Keywords (texto de venta)
            "sale_ok",            // → filtro: solo productos vendibles

            // ── Campos opcionales (descomenta si tu Odoo los tiene) ──
            // "x_quality_rating",   // → QualityRating (campo personalizado)
            // "x_specification",    // → Specification (campo personalizado)
            // "x_presentation",     // → Presentation (campo personalizado)
            // "x_is_on_sale",       // → IsOnSale (campo personalizado)
            // "x_sale_price",       // → SalePrice (campo personalizado)
        ];

        public OdooConnector(
            HttpClient http,
            SupplierCatalogDbContext db,
            IOptions<DataSourceSettings> config,
            ILogger<OdooConnector> logger)
        {
            _http = http;
            _db = db;
            _config = config.Value;
            _logger = logger;
        }

        /// <summary>
        /// Responde si el conector está disponible para sincronizar. En este caso, verificamos que la URL base de Odoo esté configurada.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<bool> IsAvailableAsync(CancellationToken ct)
        {
            var url = _config.BaseUrl.TrimEnd('/');
            return await Task.FromResult(!string.IsNullOrWhiteSpace(url));
        }


        /// <summary>
        /// Responsable de sincronizar los productos desde Odoo. El proceso es:
        /// 1. Autenticación en Odoo.
        /// 2. Lectura de productos vendibles.
        /// 3. Actualización o creación de productos en la base de datos local.
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public async Task<int> SyncProductsAsync(CancellationToken ct)
        {
            var url = _config.BaseUrl.TrimEnd('/');
            var database = _config.Database;
            var username = _config.Username;
            var password = _config.Password;

            // ── PASO 1: Autenticación ──────────────────────────────────────────────
            _logger.LogInformation("Conectando a Odoo en {Url}, base de datos: {Db}...", url, database);

            // Odoo devuelve un uid (entero) si la autenticación es exitosa, o false/null si falla.
            var uid = await AuthenticateAsync(url, database, username, password, ct);
            _logger.LogInformation("Autenticado en Odoo como uid={Uid}.", uid);

            // ── PASO 2: Leer productos vendibles ───────────────────────────────────
            var products = await ReadProductsAsync(url, database, uid, password, ct);
            foreach (var p in products)
            {
                var code = GetStringProperty(p, "default_code") ?? "sin_codigo";
                var name = GetStringProperty(p, "name") ?? "sin_nombre";
                _logger.LogDebug("Producto Odoo leído: {Code} - {Name}", code, name);
            }
            _logger.LogInformation("Leídos {Count} productos de Odoo.", products.Count);

            // ── PASO 3: Mapear y hacer upsert en BD local ──────────────────────────
            var imported = 0; // Contador de productos importados o actualizados
            foreach (var p in products)
            {
                try
                {
                    if (!GetBoolProperty(p, "sale_ok"))
                    {
                        continue;
                    }

                    var code = GetStringProperty(p, "default_code")?.Trim();
                    if (string.IsNullOrWhiteSpace(code)) continue;

                    var existing = _db.Products.FirstOrDefault(x => x.ItemCode == code);
                    var product = existing ?? new CatalogProduct();

                    product.ItemCode = code;
                    product.Description = p.GetProperty("name").GetString() ?? "";
                    product.Category = GetArrayNameProperty(p, "categ_id")?.ToLowerInvariant() ?? "general";
                    product.UnitPrice = GetDecimalProperty(p, "list_price");
                    product.Unit = GetArrayNameProperty(p, "uom_id") ?? "ud";
                    var qtyAvailable = GetNullableDecimalProperty(p, "qty_available");
                    product.AvailableStock = qtyAvailable.HasValue ? (int)qtyAvailable.Value : null;
                    product.Keywords = GetStringProperty(p, "description_sale") ?? "";
                    product.Currency = "EUR"; // Odoo no devuelve moneda en product.product, asumimos EUR o configúralo según tu caso
                    product.UpdatedAt = DateTime.UtcNow;
                    product.IsActive = true;

                    // ── Mapeo de campos personalizados (descomenta según tu Odoo) ──
                    // product.QualityRating = GetNullableIntProperty(p, "x_quality_rating");
                    // product.Specification = GetStringProperty(p, "x_specification");
                    // product.Presentation = GetStringProperty(p, "x_presentation");
                    // product.IsOnSale = GetBoolProperty(p, "x_is_on_sale");
                    // product.SalePrice = GetNullableDecimalProperty(p, "x_sale_price");

                    if (existing is null)
                        _db.Products.Add(product);

                    imported++;
                }
                catch (Exception ex)
                {
                    var code = GetStringProperty(p, "default_code") ?? "desconocido";
                    _logger.LogWarning(ex, "Error mapeando producto Odoo con código '{Code}'. Se omite.", code);
                }
                
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Sincronización Odoo completada: {Count} productos.", imported);
            return imported;
        }

        /// <summary>
        /// Responsable de encontrar un producto específico por su código. 
        /// En este conector, delegamos esta búsqueda a la base de datos local, 
        /// ya que el catálogo se sincroniza periódicamente desde Odoo.
        /// </summary>
        /// <param name="itemCode"></param>
        /// <param name="ct"></param>
        /// <returns></returns>
        public Task<CatalogProduct?> FindProductAsync(string itemCode, CancellationToken ct) =>
            Task.FromResult<CatalogProduct?>(null); // Delegamos a la BD local tras sync


        // ─────────────────────────────────────────────────────────────────────────
        // Métodos privados — Comunicación JSON-RPC con Odoo
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Autentica contra Odoo y devuelve el uid de sesión.
        /// </summary>
        private async Task<int> AuthenticateAsync(
            string url, string database, string username, string password, CancellationToken ct)
        {
            var payload = new
            {
                jsonrpc = "2.0",
                method = "call",
                @params = new
                {
                    service = "common",
                    method = "authenticate",
                    args = new object[] { database, username, password, new { } }
                }
            };

            var response = await _http.PostAsJsonAsync($"{url}/jsonrpc", payload, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

            if (result.TryGetProperty("error", out var error))
            {
                var message = error.GetProperty("message").GetString();
                throw new InvalidOperationException($"Error de autenticación en Odoo: {message}");
            }

            var uidElement = result.GetProperty("result");
            if (uidElement.ValueKind == JsonValueKind.False || uidElement.ValueKind == JsonValueKind.Null)
            {
                throw new InvalidOperationException(
                    "Credenciales de Odoo inválidas. Verifica Username, Password y Database en appsettings.json.");
            }

            return uidElement.GetInt32();
        }

        /// <summary>
        /// Lee los productos vendibles de Odoo vía JSON-RPC search_read.
        /// </summary>
        private async Task<List<JsonElement>> ReadProductsAsync(
            string url, string database, int uid, string password, CancellationToken ct)
        {
            var payload = new
            {
                jsonrpc = "2.0",
                method = "call",
                @params = new
                {
                    service = "object",
                    method = "execute_kw",
                    args = new object[]
                    {
                        database, uid, password,
                        "product.product", "search_read",
                        // Filtro: solo productos vendibles y activos
                        new object[] 
                        { 
                            new object[]
                            {
                                new object[] { "sale_ok", "=", true },
                                new object[] { "active", "=", true }
                            }
                        },
                        new
                        {
                            fields = OdooFields,
                            limit = 50000 // Ajusta según el tamaño de tu catálogo
                        }
                    }
                }
            };

            var response = await _http.PostAsJsonAsync($"{url}/jsonrpc", payload, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<JsonElement>(ct);

            if (result.TryGetProperty("error", out var error))
            {
                var message = error.GetProperty("message").GetString();
                throw new InvalidOperationException($"Error leyendo productos de Odoo: {message}");
            }

            return result.GetProperty("result").EnumerateArray().ToList();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers — Extracción segura de propiedades del JSON de Odoo
        // ─────────────────────────────────────────────────────────────────────────

        /// <summary>Extrae un string de una propiedad, devuelve null si es false/null en Odoo.</summary>
        private static string? GetStringProperty(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop)) return null;
            // Odoo devuelve false (booleano) cuando un campo de texto está vacío
            if (prop.ValueKind == JsonValueKind.False || prop.ValueKind == JsonValueKind.Null) return null;
            return prop.GetString();
        }

        /// <summary>Extrae un decimal de una propiedad.</summary>
        private static decimal GetDecimalProperty(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop)) return 0;
            if (prop.ValueKind == JsonValueKind.False || prop.ValueKind == JsonValueKind.Null) return 0;
            return prop.GetDecimal();
        }

        /// <summary>Extrae un bool de una propiedad.</summary>
        private static bool GetBoolProperty(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop)) return false;
            if (prop.ValueKind == JsonValueKind.True) return true;
            return false;
        }

        /// <summary>Extrae un int nullable de una propiedad.</summary>
        private static int? GetNullableIntProperty(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop)) return null;
            if (prop.ValueKind == JsonValueKind.False || prop.ValueKind == JsonValueKind.Null) return null;
            return prop.GetInt32();
        }

        /// <summary>Extrae un decimal nullable de una propiedad.</summary>
        private static decimal? GetNullableDecimalProperty(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop)) return null;
            if (prop.ValueKind == JsonValueKind.False || prop.ValueKind == JsonValueKind.Null) return null;
            return prop.GetDecimal();
        }

        /// <summary>
        /// Extrae el nombre de un campo Many2one de Odoo.
        /// En JSON-RPC, Odoo devuelve los Many2one como [id, "Nombre"].
        /// Ejemplo: categ_id → [7, "Materiales de construcción"]
        /// </summary>
        private static string? GetArrayNameProperty(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out var prop)) return null;
            if (prop.ValueKind == JsonValueKind.False || prop.ValueKind == JsonValueKind.Null) return null;
            if (prop.ValueKind != JsonValueKind.Array) return prop.GetString();

            var items = prop.EnumerateArray().ToList();
            return items.Count >= 2 ? items[1].GetString() : null;
        }
    }
}
