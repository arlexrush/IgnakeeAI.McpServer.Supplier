using IgnakeeAI.McpServer.Supplier.Application.Contracts;
using IgnakeeAI.McpServer.Supplier.Application.Interfaces;
using IgnakeeAI.McpServer.Supplier.Domain.Entities;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Ecommerce.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Ecommerce
{
    /// <summary>
    /// Adaptador HTTP autenticado que conecta con la API de inventario del ecommerce.
    ///
    /// AUTENTICACIÓN: envía un JWT en el encabezado Authorization (esquema Bearer).
    /// La identidad técnica debe tener el rol INVENTORY_READER (o ADMIN para break-glass).
    /// No se incluyen credenciales en mensajes de error ni en logs.
    ///
    /// ENDPOINTS:
    ///   GET /api/v1/inventory/{productCode}                           → producto individual
    ///   GET /api/v1/inventory?pageIndex={n}&amp;pageSize={n}              → catálogo paginado (PaginationVm)
    ///
    /// COMPORTAMIENTO:
    ///   - 404 → resultado funcional "no encontrado" (null / lista vacía).
    ///   - 401/403 → EcommerceAuthException (sin exponer el token).
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
                _logger.LogError("Error de autenticación/autorización en ecommerce ({StatusCode}). " +
                    "Revisa EcommerceInventory:BearerToken y el rol INVENTORY_READER.",
                    (int)response.StatusCode);
                throw new EcommerceAuthException(
                    $"Autenticación rechazada por el ecommerce (HTTP {(int)response.StatusCode}). " +
                    "Revisa EcommerceInventory:BearerToken y el rol INVENTORY_READER.");
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
        public async Task<EcommerceCatalogPage> GetCatalogPageAsync(
            int pageIndex, int pageSize, CancellationToken ct = default)
        {
            if (!IsEnabled)
                return new EcommerceCatalogPage([], pageIndex, 0);

            var basePath = _options.CatalogSyncPath.TrimEnd('/');
            var separator = basePath.Contains('?') ? "&" : "?";
            var path = $"{basePath}{separator}pageIndex={pageIndex}&pageSize={pageSize}";

            _logger.LogDebug("Solicitando página {PageIndex} del catálogo ecommerce (pageSize={PageSize}).", pageIndex, pageSize);

            HttpResponseMessage response;
            try
            {
                using var request = BuildRequest(HttpMethod.Get, path);
                response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || !ct.IsCancellationRequested)
            {
                _logger.LogWarning("Timeout al leer página {PageIndex} del catálogo ecommerce.", pageIndex);
                throw new EcommerceCommunicationException(
                    $"Timeout al leer la página {pageIndex} del catálogo ecommerce.", ex);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning("Error de red al leer catálogo ecommerce página {PageIndex}: {Type}.", pageIndex, ex.GetType().Name);
                throw new EcommerceCommunicationException(
                    $"Error de red al leer la página {pageIndex} del catálogo ecommerce.", ex);
            }

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new EcommerceCatalogPage([], pageIndex, 0);

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                _logger.LogError("Error de autenticación/autorización en ecommerce ({StatusCode}).",
                    (int)response.StatusCode);
                throw new EcommerceAuthException(
                    $"Autenticación rechazada por el ecommerce (HTTP {(int)response.StatusCode}). " +
                    "Revisa EcommerceInventory:BearerToken y el rol INVENTORY_READER.");
            }

            response.EnsureSuccessStatusCode();

            EcommerceCatalogPageDto? pageDto;
            try
            {
                pageDto = await response.Content.ReadFromJsonAsync<EcommerceCatalogPageDto>(_jsonOptions, ct);
            }
            catch (JsonException ex)
            {
                _logger.LogError("Respuesta JSON malformada al leer página {PageIndex} del catálogo ecommerce.", pageIndex);
                throw new EcommerceMappingException(
                    $"Respuesta JSON malformada al leer la página {pageIndex} del catálogo ecommerce.", ex);
            }

            if (pageDto is null)
                return new EcommerceCatalogPage([], pageIndex, 0);

            var productDtos = pageDto.Data ?? [];
            var products = new List<CatalogProduct>(productDtos.Count);
            foreach (var dto in productDtos)
            {
                if (string.IsNullOrWhiteSpace(dto.ProductCode))
                {
                    _logger.LogWarning("Producto ignorado: ProductCode vacío en página {PageIndex}.", pageIndex);
                    continue;
                }

                products.Add(MapToProduct(dto));
            }

            return new EcommerceCatalogPage(products, pageDto.PageIndex, pageDto.PageCount);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private HttpRequestMessage BuildRequest(HttpMethod method, string path)
        {
            var baseUrl = _options.BaseUrl.TrimEnd('/');
            var relPath = path.StartsWith('/') ? path : '/' + path;
            var request = new HttpRequestMessage(method, baseUrl + relPath);

            if (!string.IsNullOrWhiteSpace(_options.BearerToken))
            {
                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _options.BearerToken);
            }

            return request;
        }

        /// <summary>
        /// Convierte un DTO del ecommerce en la entidad de dominio CatalogProduct.
        /// - price null → UnitPrice 0m (sin inventar precio; 0 indica precio no disponible).
        /// - isAvailableForSale AND status "Active" → IsActive = true.
        /// - purchaseLeadTimeUnit normalizado: "hours" → días redondeando, "weeks" → ×7, default → días.
        /// </summary>
        public static CatalogProduct MapToProduct(EcommerceProductDto dto)
        {
            var description = !string.IsNullOrWhiteSpace(dto.Description)
                ? dto.Description
                : dto.ProductName ?? dto.ProductCode ?? string.Empty;

            var leadTimeDays = dto.PurchaseLeadTime.HasValue
                ? NormalizeLeadTimeToDays(dto.PurchaseLeadTime.Value, dto.PurchaseLeadTimeUnit)
                : (int?)null;

            var isActive = dto.IsAvailableForSale &&
                string.Equals(dto.Status, "Active", StringComparison.OrdinalIgnoreCase);

            return new CatalogProduct
            {
                ItemCode = dto.ProductCode!,
                Description = description,
                Category = dto.Category ?? string.Empty,
                UnitPrice = dto.Price ?? 0m,
                Currency = !string.IsNullOrWhiteSpace(dto.Currency) ? dto.Currency : "EUR",
                Unit = dto.UnitToSell ?? "ud",
                AvailableStock = dto.Stock ?? 0,
                LeadTimeDays = leadTimeDays,
                IsActive = isActive,
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
