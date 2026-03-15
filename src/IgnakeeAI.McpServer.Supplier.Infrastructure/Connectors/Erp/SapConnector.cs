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
    /// Conector para SAP Business One / SAP S/4HANA vía OData Service Layer.
    ///
    /// SAP expone catálogos de materiales via OData:
    ///   - SAP Business One: Service Layer → /b1s/v1/Items
    ///   - SAP S/4HANA: API_PRODUCT_SRV → /sap/opu/odata/sap/API_PRODUCT_SRV/A_Product
    ///
    /// CONFIGURACIÓN:
    ///   "Erp": {
    ///     "Provider": "SAP",
    ///     "Sap": {
    ///       "BaseUrl": "https://mi-sap:50000/b1s/v1",
    ///       "CompanyDb": "MI_EMPRESA",
    ///       "Username": "manager",
    ///       "Password": "password"
    ///     }
    ///   }
    /// </summary>
    public class SapConnector : IErpConnector
    {
        private readonly HttpClient _http;
        private readonly SupplierCatalogDbContext _db;
        private readonly DataSourceSettings _config;
        private readonly ILogger<SapConnector> _logger;

        public string ErpName => "SAP";

        public SapConnector(
            HttpClient http,
            SupplierCatalogDbContext db,
            IOptions<DataSourceSettings> config,
            ILogger<SapConnector> logger)
        {
            _http = http;
            _db = db;
            _config = config.Value;
            _logger = logger;
        }

        public async Task<bool> IsAvailableAsync(CancellationToken ct)
        {
            var url = _config.BaseUrl;
            return await Task.FromResult(!string.IsNullOrWhiteSpace(url));
        }

        public async Task<int> SyncProductsAsync(CancellationToken ct)
        {
            var baseUrl = _config.BaseUrl.TrimEnd('/');
            var companyDb = _config.Database;
            var username = _config.Username;
            var password = _config.Password;

            // 1. Autenticación en SAP Service Layer
            _logger.LogInformation("Conectando a SAP Service Layer en {Url}...", baseUrl);

            var loginPayload = new { CompanyDB = companyDb, UserName = username, Password = password };
            var loginResponse = await _http.PostAsJsonAsync($"{baseUrl}/Login", loginPayload, ct);
            loginResponse.EnsureSuccessStatusCode();

            // Extraer cookie de sesión y usarla por request (no en DefaultRequestHeaders)
            var sessionCookie = loginResponse.Headers
                .FirstOrDefault(h => h.Key.Equals("Set-Cookie", StringComparison.OrdinalIgnoreCase))
                .Value?.FirstOrDefault();

            _logger.LogInformation("Autenticado en SAP Service Layer.");

            // 2. Leer ítems del catálogo con paginación OData            
            var imported = 0;
            var nextUrl = $"{baseUrl}/Items?$select=ItemCode,ItemName,ItemsGroupCode,AvgStdPrice,SalesUnit,QuantityOnStock&$filter=SalesItem eq 'tYES'&$top=5000";

            while (!string.IsNullOrEmpty(nextUrl))
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, nextUrl);
                if (sessionCookie is not null)
                    request.Headers.Add("Cookie", sessionCookie);

                var itemsResponse = await _http.SendAsync(request, ct);
                itemsResponse.EnsureSuccessStatusCode();

                var itemsJson = await itemsResponse.Content.ReadFromJsonAsync<JsonElement>(ct);
                var items = itemsJson.GetProperty("value").EnumerateArray();

                foreach (var item in items)
                {
                    try
                    {
                        var code = item.GetProperty("ItemCode").GetString()?.Trim();
                        if (string.IsNullOrWhiteSpace(code)) continue;

                        var existing = _db.Products.FirstOrDefault(x => x.ItemCode == code);
                        var product = existing ?? new CatalogProduct();

                        product.ItemCode = code;
                        product.Description = item.GetProperty("ItemName").GetString() ?? "";
                        product.Category = item.TryGetProperty("ItemsGroupCode", out var grp)
                            ? $"sap-group-{grp.GetInt32()}"
                            : "general";
                        product.UnitPrice = item.TryGetProperty("AvgStdPrice", out var price)
                            && price.ValueKind != JsonValueKind.Null
                            ? price.GetDecimal()
                            : 0;
                        product.Unit = item.TryGetProperty("SalesUnit", out var unit)
                            && unit.ValueKind != JsonValueKind.Null
                            ? unit.GetString() ?? "ud"
                            : "ud";
                        product.AvailableStock = item.TryGetProperty("QuantityOnStock", out var qty)
                            && qty.ValueKind != JsonValueKind.Null
                            ? (int)qty.GetDecimal()
                            : null;
                        product.Currency = "EUR";
                        product.UpdatedAt = DateTime.UtcNow;
                        product.IsActive = true;

                        if (existing is null)
                            _db.Products.Add(product);

                        imported++;
                    }
                    catch (Exception ex)
                    {
                        var code = item.TryGetProperty("ItemCode", out var c) ? c.GetString() : "desconocido";
                        _logger.LogWarning(ex, "Error mapeando producto SAP con código '{Code}'. Se omite.", code);
                    }
                }

                // Paginación OData: seguir si hay @odata.nextLink
                nextUrl = itemsJson.TryGetProperty("odata.nextLink", out var next)
                    ? $"{baseUrl}/{next.GetString()}"
                    : itemsJson.TryGetProperty("@odata.nextLink", out var next2)
                        ? next2.GetString()
                        : null;
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Sincronización SAP completada: {Count} productos.", imported);

            // 3. Cerrar sesión SAP
            try
            {
                using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/Logout");
                if (sessionCookie is not null)
                    logoutRequest.Headers.Add("Cookie", sessionCookie);
                await _http.SendAsync(logoutRequest, ct);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error cerrando sesión SAP (no crítico).");
            }

            return imported;
        }

        public Task<CatalogProduct?> FindProductAsync(string itemCode, CancellationToken ct) =>
            Task.FromResult<CatalogProduct?>(null);
    }
}
