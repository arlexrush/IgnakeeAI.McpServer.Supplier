using IgnakeeAI.McpServer.Supplier.Application.Interfaces;
using IgnakeeAI.McpServer.Supplier.Domain.Entities;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Ecommerce.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Web;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Ecommerce
{
    /// <summary>
    /// Adaptador HTTP autenticado que conecta con la API de inventario del ecommerce.
    ///
    /// AUTENTICACIÓN: envía la clave API en el encabezado configurado en
    ///   EcommerceInventory:ApiKeyHeaderName / ApiKeyValue.
    /// No se incluyen credenciales en mensajes de error ni en logs.
    ///
    /// COMPORTAMIENTO:
    ///   - 404 → resultado funcional "no encontrado" (null / lista vacía).
    ///   - 401/403 → EcommerceAuthException (sin exponer la clave).
    ///   - Timeout / error de red → EcommerceCommunicationException.
    ///   - JSON malformado → EcommerceMappingException.
    ///   - Respuesta con ProductCode vacío → producto descartado con advertencia.
    /// </summary>
    public sealed class EcommerceInventoryConnector : IEcommerceInventoryClient
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly HttpClient _http;
        private readonly EcommerceInventoryOptions _options;
        private readonly ILogger<EcommerceInventoryConnector> _logger;

        public bool IsEnabled => _options.Enabled &&
            !string.IsNullOrWhiteSpace(_options.BaseUrl);

        public EcommerceInventoryConnector(
            HttpClient http,
            IOptions<EcommerceInventoryOptions> options,
            ILogger<EcommerceInventoryConnector> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<CatalogProduct?> GetProductByCodeAsync(
            string productCode, CancellationToken ct = default)
        {
            if (!IsEnabled)
                return null;

            if (string.IsNullOrWhiteSpace(productCode))
                throw new ArgumentException("productCode es obligatorio.", nameof(productCode));

            var path = _options.ProductLookupPath
                .Replace("{productCode}", Uri.EscapeDataString(productCode));

            _logger.LogDebug("Consultando producto ecommerce: {Path}", path);

            HttpResponseMessage response;
            try
            {
                using var request = BuildRequest(HttpMethod.Get, path);
                response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || !ct.IsCancellationRequested)
            {
                _logger.LogWarning("Timeout al consultar producto ecommerce {ProductCode}.", productCode);
                throw new EcommerceCommunicationException(
                    $"Timeout al consultar producto '{productCode}' en el ecommerce.", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Error de red al consultar producto ecommerce {ProductCode}: {Type}.",
                    productCode, ex.GetType().Name);
                throw new EcommerceCommunicationException(
                    $"Error de red al consultar el ecommerce para producto '{productCode}'.", ex);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                _logger.LogDebug("Producto '{ProductCode}' no encontrado en ecommerce.", productCode);
                return null;
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _logger.LogError("Error de autenticación/autorización en ecommerce ({StatusCode}).",
                    (int)response.StatusCode);
                throw new EcommerceAuthException(
                    $"Autenticación rechazada por el ecommerce (HTTP {(int)response.StatusCode}). " +
                    "Revisa EcommerceInventory:ApiKeyValue.");
            }

            response.EnsureSuccessStatusCode();

            EcommerceProductDto? dto;
            try
            {
                dto = await response.Content.ReadFromJsonAsync<EcommerceProductDto>(_jsonOptions, ct);
            }
            catch (JsonException ex)
            {
                _logger.LogError("Respuesta JSON malformada al leer producto ecommerce {ProductCode}.", productCode);
                throw new EcommerceMappingException(
                    $"Respuesta JSON malformada al leer producto '{productCode}' del ecommerce.", ex);
            }

            if (dto is null || string.IsNullOrWhiteSpace(dto.ProductCode))
            {
                _logger.LogWarning("El ecommerce devolvió un producto sin ProductCode para código '{ProductCode}'.",
                    productCode);
                return null;
            }

            return MapToProduct(dto);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<CatalogProduct>> GetCatalogPageAsync(
            int page, int pageSize, CancellationToken ct = default)
        {
            if (!IsEnabled)
                return [];

            var basePath = _options.CatalogSyncPath.TrimEnd('/');
            var separator = basePath.Contains('?') ? "&" : "?";
            var path = $"{basePath}{separator}page={page}&pageSize={pageSize}&status=active";

            _logger.LogDebug("Solicitando página {Page} del catálogo ecommerce (pageSize={PageSize}).", page, pageSize);

            HttpResponseMessage response;
            try
            {
                using var request = BuildRequest(HttpMethod.Get, path);
                response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || !ct.IsCancellationRequested)
            {
                _logger.LogWarning("Timeout al leer página {Page} del catálogo ecommerce.", page);
                throw new EcommerceCommunicationException(
                    $"Timeout al leer la página {page} del catálogo ecommerce.", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Error de red al leer catálogo ecommerce página {Page}: {Type}.", page, ex.GetType().Name);
                throw new EcommerceCommunicationException(
                    $"Error de red al leer la página {page} del catálogo ecommerce.", ex);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
                return [];

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _logger.LogError("Error de autenticación/autorización en ecommerce ({StatusCode}).",
                    (int)response.StatusCode);
                throw new EcommerceAuthException(
                    $"Autenticación rechazada por el ecommerce (HTTP {(int)response.StatusCode}).");
            }

            response.EnsureSuccessStatusCode();

            EcommerceCatalogPageDto? pageDto;
            try
            {
                pageDto = await response.Content.ReadFromJsonAsync<EcommerceCatalogPageDto>(_jsonOptions, ct);
            }
            catch (JsonException ex)
            {
                _logger.LogError("Respuesta JSON malformada al leer página {Page} del catálogo ecommerce.", page);
                throw new EcommerceMappingException(
                    $"Respuesta JSON malformada al leer la página {page} del catálogo ecommerce.", ex);
            }

            if (pageDto?.Items is null or { Count: 0 })
                return [];

            var products = new List<CatalogProduct>(pageDto.Items.Count);
            foreach (var dto in pageDto.Items)
            {
                if (string.IsNullOrWhiteSpace(dto.ProductCode))
                {
                    _logger.LogWarning("Producto ignorado: ProductCode vacío en página {Page}.", page);
                    continue;
                }

                products.Add(MapToProduct(dto));
            }

            return products;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private HttpRequestMessage BuildRequest(HttpMethod method, string path)
        {
            var baseUrl = _options.BaseUrl.TrimEnd('/');
            var relPath = path.StartsWith('/') ? path : '/' + path;
            var request = new HttpRequestMessage(method, baseUrl + relPath);

            if (!string.IsNullOrWhiteSpace(_options.ApiKeyHeaderName) &&
                !string.IsNullOrWhiteSpace(_options.ApiKeyValue))
            {
                request.Headers.TryAddWithoutValidation(
                    _options.ApiKeyHeaderName, _options.ApiKeyValue);
            }

            return request;
        }

        /// <summary>
        /// Convierte un DTO del ecommerce en la entidad de dominio CatalogProduct.
        /// purchaseLeadTimeUnit normalizado: "hours" → días redondeando, "weeks" → ×7, default → días.
        /// </summary>
        public static CatalogProduct MapToProduct(EcommerceProductDto dto)
        {
            var description = !string.IsNullOrWhiteSpace(dto.Description)
                ? dto.Description
                : dto.ProductName ?? dto.ProductCode ?? string.Empty;

            var leadTimeDays = dto.PurchaseLeadTime.HasValue
                ? NormalizeLeadTimeToDays(dto.PurchaseLeadTime.Value, dto.PurchaseLeadTimeUnit)
                : (int?)null;

            return new CatalogProduct
            {
                ItemCode = dto.ProductCode!,
                Description = description,
                Category = dto.Category ?? string.Empty,
                UnitPrice = dto.Price,
                Currency = !string.IsNullOrWhiteSpace(dto.Currency) ? dto.Currency : "EUR",
                Unit = dto.UnitToSell ?? "ud",
                AvailableStock = dto.Stock ?? 0,
                LeadTimeDays = leadTimeDays,
                IsActive = string.Equals(dto.Status, "active", StringComparison.OrdinalIgnoreCase),
                UpdatedAt = DateTime.UtcNow
            };
        }

        private static int NormalizeLeadTimeToDays(int value, string? unit) =>
            unit?.ToLowerInvariant() switch
            {
                "hours" => (int)Math.Ceiling(value / 24.0),
                "weeks" => value * 7,
                _ => value   // "days" o cualquier otro valor → días directamente
            };
    }
}
