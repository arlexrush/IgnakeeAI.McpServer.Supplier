using IgnakeeAI.McpServer.Supplier.Application.Contracts;
using IgnakeeAI.McpServer.Supplier.Application.Interfaces;
using IgnakeeAI.McpServer.Supplier.Domain.Entities;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors;

public sealed class EcommerceInventoryConnector : IEcommerceInventoryService
{
    private readonly HttpClient _httpClient;
    private readonly SupplierCatalogDbContext _db;
    private readonly EcommerceInventoryOptions _options;
    private readonly ILogger<EcommerceInventoryConnector> _logger;

    public EcommerceInventoryConnector(
        HttpClient httpClient,
        SupplierCatalogDbContext db,
        IOptions<EcommerceInventoryOptions> options,
        ILogger<EcommerceInventoryConnector> logger)
    {
        _httpClient = httpClient;
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => _options.Enabled;

    public async Task<CatalogProduct?> FindByCodeAsync(string itemCode, CancellationToken ct = default)
    {
        if (!IsEnabled)
            return null;

        if (string.IsNullOrWhiteSpace(itemCode))
            throw new ArgumentException("itemCode es obligatorio.", nameof(itemCode));

        var path = _options.ProductLookupPathTemplate.Replace(
            "{productCode}",
            Uri.EscapeDataString(itemCode.Trim()),
            StringComparison.Ordinal);

        using var response = await SendAsync(path, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessfulResponseAsync(response);

        using var document = await ReadDocumentAsync(response, ct);
        var payload = ExtractSingleProduct(document.RootElement);
        return MapProduct(payload);
    }

    public async Task<EcommerceInventorySyncResult> SyncCatalogAsync(CancellationToken ct = default)
    {
        if (!IsEnabled)
            throw new InvalidOperationException("La integración EcommerceInventory está deshabilitada.");

        var productsRead = 0;
        var productsCreated = 0;
        var productsUpdated = 0;
        var productsRejected = 0;
        var page = 1;
        var hasMore = true;

        while (hasMore)
        {
            var path = _options.CatalogSyncPathTemplate
                .Replace("{page}", page.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{pageSize}", _options.CatalogSyncPageSize.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);

            using var response = await SendAsync(path, ct);
            await EnsureSuccessfulResponseAsync(response);

            using var document = await ReadDocumentAsync(response, ct);
            var items = ExtractCatalogItems(document.RootElement);
            productsRead += items.Count;

            foreach (var item in items)
            {
                try
                {
                    var mappedProduct = MapProduct(item);
                    var existing = await _db.Products
                        .FirstOrDefaultAsync(product => product.ItemCode == mappedProduct.ItemCode, ct);

                    if (existing is null)
                    {
                        _db.Products.Add(mappedProduct);
                        productsCreated++;
                    }
                    else
                    {
                        UpdateExistingProduct(existing, mappedProduct);
                        productsUpdated++;
                    }
                }
                catch (EcommerceInventoryException ex) when (ex.Kind == EcommerceInventoryFailureKind.InvalidResponse)
                {
                    productsRejected++;
                    _logger.LogWarning(
                        "Producto ecommerce rechazado durante la sincronización de catálogo: {Message}",
                        ex.Message);
                }
            }

            await _db.SaveChangesAsync(ct);
            hasMore = ShouldContinuePaging(document.RootElement, items.Count, page);
            page++;
        }

        return new EcommerceInventorySyncResult(
            productsRead,
            productsCreated,
            productsUpdated,
            productsRejected);
    }

    private async Task<HttpResponseMessage> SendAsync(string relativePath, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relativePath);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation(
            _options.AuthenticationHeaderName,
            _options.AuthenticationHeaderValue);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_options.RequestTimeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

        try
        {
            return await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                linkedCts.Token);
        }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested && timeoutCts.IsCancellationRequested)
        {
            throw new EcommerceInventoryException(
                EcommerceInventoryFailureKind.Timeout,
                "La consulta al inventario ecommerce superó el tiempo de espera configurado.",
                ex);
        }
        catch (HttpRequestException ex)
        {
            throw new EcommerceInventoryException(
                EcommerceInventoryFailureKind.Technical,
                "No se pudo completar la comunicación con el inventario ecommerce.",
                ex);
        }
    }

    private static async Task<JsonDocument> ReadDocumentAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        }
        catch (JsonException ex)
        {
            throw new EcommerceInventoryException(
                EcommerceInventoryFailureKind.InvalidResponse,
                "La respuesta del inventario ecommerce no contiene JSON válido.",
                ex);
        }
    }

    private static Task EnsureSuccessfulResponseAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return Task.CompletedTask;

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new EcommerceInventoryException(
                EcommerceInventoryFailureKind.Authentication,
                "El servicio de inventario ecommerce rechazó las credenciales configuradas.");
        }

        throw new EcommerceInventoryException(
            EcommerceInventoryFailureKind.Technical,
            $"El servicio de inventario ecommerce devolvió el estado HTTP {(int)response.StatusCode}.");
    }

    private static JsonElement ExtractSingleProduct(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object)
        {
            if (TryGetObjectProperty(root, "product", out var product))
                return product;

            if (TryGetObjectProperty(root, "data", out var data))
                return data;

            return root;
        }

        throw new EcommerceInventoryException(
            EcommerceInventoryFailureKind.InvalidResponse,
            "La respuesta del inventario ecommerce no contiene un producto válido.");
    }

    private static List<JsonElement> ExtractCatalogItems(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Array)
            return root.EnumerateArray().Select(item => item.Clone()).ToList();

        if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var propertyName in new[] { "items", "products", "data" })
            {
                if (root.TryGetProperty(propertyName, out var itemsElement) &&
                    itemsElement.ValueKind == JsonValueKind.Array)
                {
                    return itemsElement.EnumerateArray().Select(item => item.Clone()).ToList();
                }
            }
        }

        throw new EcommerceInventoryException(
            EcommerceInventoryFailureKind.InvalidResponse,
            "La respuesta del catálogo ecommerce no contiene una colección de productos válida.");
    }

    private bool ShouldContinuePaging(JsonElement root, int itemCount, int page)
    {
        if (TryGetBoolean(root, "hasMore", out var hasMore))
            return hasMore;

        if (TryGetInt32(root, "nextPage", out var nextPage))
            return nextPage > page;

        if (TryGetInt32(root, "totalPages", out var totalPages))
            return page < totalPages;

        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("pagination", out var pagination) &&
            pagination.ValueKind == JsonValueKind.Object)
        {
            if (TryGetBoolean(pagination, "hasMore", out hasMore))
                return hasMore;

            if (TryGetInt32(pagination, "nextPage", out nextPage))
                return nextPage > page;

            if (TryGetInt32(pagination, "totalPages", out totalPages))
                return page < totalPages;
        }

        return itemCount >= _options.CatalogSyncPageSize;
    }

    private static CatalogProduct MapProduct(JsonElement element)
    {
        var itemCode = RequireString(element, "productCode");
        var productName = GetString(element, "productName");
        var description = FirstNonEmpty(GetString(element, "description"), productName);
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new EcommerceInventoryException(
                EcommerceInventoryFailureKind.InvalidResponse,
                $"El producto '{itemCode}' no contiene descripción ni nombre.");
        }

        var category = FirstNonEmpty(GetString(element, "category"), "general")!.Trim().ToLowerInvariant();
        var unit = FirstNonEmpty(GetString(element, "unitToSell"), "ud")!.Trim();
        var currency = RequireString(element, "currency").Trim().ToUpperInvariant();
        var price = RequireDecimal(element, "price");
        var stock = GetNullableDecimal(element, "stock");
        var leadTime = ConvertLeadTime(
            GetNullableDecimal(element, "purchaseLeadTime"),
            GetString(element, "purchaseLeadTimeUnit"));
        var status = GetString(element, "status");

        var product = new CatalogProduct
        {
            ItemCode = itemCode.Trim(),
            Description = description.Trim(),
            Category = category,
            Keywords = BuildKeywords(itemCode, productName, category),
            Unit = unit,
            UnitPrice = price,
            Currency = currency,
            AvailableStock = stock.HasValue ? (int)Math.Floor(stock.Value) : null,
            LeadTimeDays = leadTime,
            Specification = null,
            Presentation = null,
            ProductUrl = null,
            IsOnSale = false,
            SalePrice = null,
            QualityRating = null,
            PackSize = null,
            PackPrice = null,
            UpdatedAt = DateTime.UtcNow,
            IsActive = IsActiveStatus(status)
        };

        if (!CatalogProductImportValidator.TryValidate(product, out var rejectionReason))
        {
            throw new EcommerceInventoryException(
                EcommerceInventoryFailureKind.InvalidResponse,
                rejectionReason ?? "Producto ecommerce inválido.");
        }

        return product;
    }

    private static void UpdateExistingProduct(CatalogProduct target, CatalogProduct source)
    {
        target.Description = source.Description;
        target.Category = source.Category;
        target.Keywords = source.Keywords;
        target.Unit = source.Unit;
        target.UnitPrice = source.UnitPrice;
        target.Currency = source.Currency;
        target.PackSize = source.PackSize;
        target.PackPrice = source.PackPrice;
        target.Specification = source.Specification;
        target.Presentation = source.Presentation;
        target.AvailableStock = source.AvailableStock;
        target.LeadTimeDays = source.LeadTimeDays;
        target.ProductUrl = source.ProductUrl;
        target.IsOnSale = source.IsOnSale;
        target.SalePrice = source.SalePrice;
        target.QualityRating = source.QualityRating;
        target.IsSubstitute = source.IsSubstitute;
        target.ValidUntil = source.ValidUntil;
        target.UpdatedAt = source.UpdatedAt;
        target.IsActive = source.IsActive;
    }

    private static bool TryGetObjectProperty(JsonElement element, string propertyName, out JsonElement propertyValue)
    {
        if (element.TryGetProperty(propertyName, out propertyValue) &&
            propertyValue.ValueKind == JsonValueKind.Object)
        {
            return true;
        }

        propertyValue = default;
        return false;
    }

    private static bool TryGetBoolean(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (!element.TryGetProperty(propertyName, out var property))
            return false;

        if (property.ValueKind == JsonValueKind.True)
        {
            value = true;
            return true;
        }

        if (property.ValueKind == JsonValueKind.False)
            return true;

        if (property.ValueKind == JsonValueKind.String &&
            bool.TryParse(property.GetString(), out var parsed))
        {
            value = parsed;
            return true;
        }

        return false;
    }

    private static bool TryGetInt32(JsonElement element, string propertyName, out int value)
    {
        value = default;
        return element.TryGetProperty(propertyName, out var property) &&
               TryReadInt32(property, out value);
    }

    private static string RequireString(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        throw new EcommerceInventoryException(
            EcommerceInventoryFailureKind.InvalidResponse,
            $"La propiedad '{propertyName}' es obligatoria en la respuesta ecommerce.");
    }

    private static decimal RequireDecimal(JsonElement element, string propertyName)
    {
        var value = GetNullableDecimal(element, propertyName);
        if (value.HasValue)
            return value.Value;

        throw new EcommerceInventoryException(
            EcommerceInventoryFailureKind.InvalidResponse,
            $"La propiedad '{propertyName}' es obligatoria en la respuesta ecommerce.");
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString(),
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => null
        };
    }

    private static decimal? GetNullableDecimal(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        return TryReadDecimal(property, out var value) ? value : null;
    }

    private static bool TryReadInt32(JsonElement property, out int value)
    {
        if (property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value))
            return true;

        if (property.ValueKind == JsonValueKind.String &&
            int.TryParse(property.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryReadDecimal(JsonElement property, out decimal value)
    {
        if (property.ValueKind == JsonValueKind.Number && property.TryGetDecimal(out value))
            return true;

        if (property.ValueKind == JsonValueKind.String &&
            decimal.TryParse(property.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static int? ConvertLeadTime(decimal? leadTime, string? leadTimeUnit)
    {
        if (!leadTime.HasValue)
            return null;

        if (leadTime.Value < 0)
        {
            throw new EcommerceInventoryException(
                EcommerceInventoryFailureKind.InvalidResponse,
                "purchaseLeadTime no puede ser negativo.");
        }

        var normalizedUnit = leadTimeUnit?.Trim().ToLowerInvariant();
        return normalizedUnit switch
        {
            null or "" or "day" or "days" or "día" or "días" => (int)Math.Ceiling(leadTime.Value),
            "hour" or "hours" or "hora" or "horas" => (int)Math.Ceiling(leadTime.Value / 24m),
            "week" or "weeks" or "semana" or "semanas" => (int)Math.Ceiling(leadTime.Value * 7m),
            "month" or "months" or "mes" or "meses" => (int)Math.Ceiling(leadTime.Value * 30m),
            _ => null
        };
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static string BuildKeywords(string itemCode, string? productName, string category)
    {
        var values = new[] { itemCode, productName, category }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim());

        return string.Join(',', values);
    }

    private static bool IsActiveStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
            return true;

        return status.Trim().ToLowerInvariant() is not ("inactive" or "disabled" or "archived" or "discontinued");
    }
}

internal sealed class DisabledEcommerceInventoryService : IEcommerceInventoryService
{
    public bool IsEnabled => false;

    public Task<CatalogProduct?> FindByCodeAsync(string itemCode, CancellationToken ct = default) =>
        Task.FromResult<CatalogProduct?>(null);

    public Task<EcommerceInventorySyncResult> SyncCatalogAsync(CancellationToken ct = default) =>
        throw new InvalidOperationException("La integración EcommerceInventory está deshabilitada.");
}
