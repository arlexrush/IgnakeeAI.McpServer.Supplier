using IgnakeeAI.McpServer.Supplier.Application.Interfaces;
using IgnakeeAI.McpServer.Supplier.Application.Models;
using IgnakeeAI.McpServer.Supplier.Application.Contracts;
using IgnakeeAI.McpServer.Supplier.Domain.Entities;
using IgnakeeAI.McpServer.Supplier.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace IgnakeeAI.McpServer.Supplier.Application.Services
{
    /// <summary>
    /// Servicio de aplicación que orquesta las búsquedas en el catálogo.
    /// Encapsula la lógica de búsqueda por criterios de sustitución.
    /// Las tools MCP delegan aquí toda la lógica; no acceden directamente al repositorio.
    /// </summary>
    public class CatalogSearchService
    {
        private readonly ICatalogRepository _catalog;
        private readonly ISupplierConfig _supplierConfig;
        private readonly IEcommerceInventoryClient? _ecommerce;
        private readonly ILogger<CatalogSearchService>? _logger;

        public CatalogSearchService(ICatalogRepository catalog, ISupplierConfig supplierConfig,
            IEcommerceInventoryClient? ecommerce = null,
            ILogger<CatalogSearchService>? logger = null)
        {
            _catalog = catalog;
            _supplierConfig = supplierConfig;
            _ecommerce = ecommerce;
            _logger = logger;
        }

        /// <summary>Busca un producto por código o descripción y devuelve el resultado de precio.</summary>
        public async Task<PriceResult> GetPriceAsync(
            string itemDescription, string? itemCode, string currency, CancellationToken ct)
        {
            ValidateDescription(itemDescription);
            ValidateCurrency(currency);
            CatalogProduct? product = null;

            // 1. Búsqueda exacta por código
            if (!string.IsNullOrWhiteSpace(itemCode))
            {
                product = await _catalog.FindByCodeAsync(itemCode, ct);
            }

            // 2. Búsqueda por descripción
            if (product is null && !string.IsNullOrWhiteSpace(itemDescription))
            {
                var terms = ExtractSearchTerms(itemDescription);
                product = await _catalog.FindByDescriptionAsync(terms, ct);
            }

            if (product is null)
            {
                return new PriceResult { Found = false, Currency = currency.ToUpperInvariant() };
            }

            return new PriceResult
            {
                Found = true,
                ItemCode = product.ItemCode,
                Description = product.Description,
                UnitPrice = product.EffectivePrice,
                IsOnSale = product.IsOnSale && product.SalePrice.HasValue,
                OriginalPrice = product.IsOnSale && product.SalePrice.HasValue
                    ? product.UnitPrice
                    : null,
                Currency = product.Currency,
                Unit = product.Unit,
                PackSize = product.PackSize,
                PackPrice = product.PackPrice,
                ValidUntil = product.ValidUntil.HasValue ? new DateTimeOffset(product.ValidUntil.Value) : null,
                ContactEmail = _supplierConfig.ContactEmail,
                ContactPhone = _supplierConfig.ContactPhone,
                ContactAddress = _supplierConfig.ContactAddress,
                VendorName = _supplierConfig.VendorName
            };
        }

        /// <summary>Busca alternativas según criterio de sustitución.</summary>
        public async Task<IReadOnlyList<AlternativeMatch>> SearchAlternativesAsync(
            string itemDescription, string? category, SubstitutionCriteria criteria,
            decimal? requiredQuantity, int maxResults, string currency, CancellationToken ct)
        {
            ValidateDescription(itemDescription);
            ValidateCurrency(currency);
            if (maxResults is < 1 or > 100)
                throw new ArgumentOutOfRangeException(nameof(maxResults), "Debe estar entre 1 y 100.");
            if (requiredQuantity is <= 0)
                throw new ArgumentOutOfRangeException(nameof(requiredQuantity), "Debe ser mayor que cero.");
            // Inferir categoría si no se proporcionó
            if (string.IsNullOrWhiteSpace(category))
            {
                var terms = ExtractSearchTerms(itemDescription);
                category = await _catalog.InferCategoryAsync(terms, ct);
            }

            if (string.IsNullOrWhiteSpace(category))
            {
                return Array.Empty<AlternativeMatch>();
            }

            return criteria switch
            {
                SubstitutionCriteria.Cheaper => await FindCheaperAsync(category, itemDescription, maxResults, ct),
                SubstitutionCriteria.Better => await FindBetterAsync(category, maxResults, ct),
                SubstitutionCriteria.OnSale => await FindOnSaleAsync(category, maxResults, ct),
                SubstitutionCriteria.OptimalPack => await FindOptimalPackAsync(category, requiredQuantity, maxResults, ct),
                _ => await FindAllAsync(category, itemDescription, requiredQuantity, maxResults, ct)
            };
        }

        /// <summary>
        /// Consulta disponibilidad por código.
        /// Comportamiento híbrido:
        ///   1. Si el conector ecommerce está habilitado, consulta disponibilidad en tiempo real.
        ///   2. Si el ecommerce no está disponible o no encuentra el producto, cae en el catálogo local.
        /// </summary>
        public async Task<AvailabilityResult> CheckAvailabilityAsync(string itemCode, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(itemCode))
                throw new ArgumentException("itemCode es obligatorio.", nameof(itemCode));

            // Consulta en tiempo real al ecommerce cuando está habilitado
            if (_ecommerce is { IsEnabled: true })
            {
                try
                {
                    var liveProduct = await _ecommerce.GetProductByCodeAsync(itemCode, ct);
                    if (liveProduct is not null)
                    {
                        return new AvailabilityResult
                        {
                            Found = true,
                            ItemCode = liveProduct.ItemCode,
                            AvailableStock = Math.Max(0, (decimal)(liveProduct.AvailableStock ?? 0)),
                            LeadTimeDays = Math.Max(0, liveProduct.LeadTimeDays ?? 0),
                            Message = (liveProduct.AvailableStock ?? 0) > 0 ? "Disponible" : "Sin stock"
                        };
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    var failureKind = (ex as IEcommerceFailure)?.FailureKind ?? EcommerceFailureKind.Unknown;
                    _logger?.LogWarning(ex,
                        "Fallback al catálogo local para disponibilidad de {ItemCode} tras fallo de ecommerce {FailureKind}.",
                        itemCode, failureKind);
                }
            }

            var product = await _catalog.FindByCodeAsync(itemCode, ct);
            if (product is null)
            {
                return new AvailabilityResult { Found = false, ItemCode = itemCode };
            }

            return new AvailabilityResult
            {
                Found = true,
                ItemCode = product.ItemCode,
                AvailableStock = Math.Max(0, (decimal)(product.AvailableStock ?? 0)),
                LeadTimeDays = Math.Max(0, product.LeadTimeDays ?? 0),
                Message = (product.AvailableStock ?? 0) > 0 ? "Disponible" : "Sin stock"
            };
        }

        private static void ValidateDescription(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("itemDescription es obligatorio.", nameof(value));
        }

        private static void ValidateCurrency(string currency)
        {
            if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3 ||
                currency.Any(c => !char.IsAsciiLetter(c)))
                throw new ArgumentException("currency debe ser un código ISO 4217 de tres letras.", nameof(currency));
        }

        // ── Estrategias de búsqueda ─────────────────────────────────

        private async Task<IReadOnlyList<AlternativeMatch>> FindCheaperAsync(
            string category, string description, int max, CancellationToken ct)
        {
            var terms = ExtractSearchTerms(description);
            var refProduct = await _catalog.FindByDescriptionAsync(terms, ct);
            var refPrice = refProduct?.UnitPrice ?? decimal.MaxValue;

            var products = await _catalog.FindCheaperInCategoryAsync(category, refPrice, max, ct);

            return products.Select(p => new AlternativeMatch(p,
                $"Más económico: {p.EffectivePrice} vs {refPrice} {p.Currency}/{p.Unit} " +
                $"(ahorro {(refPrice > 0 ? Math.Round((1 - p.EffectivePrice / refPrice) * 100, 1) : 0)}%)"))
                .ToList();
        }

        private async Task<IReadOnlyList<AlternativeMatch>> FindBetterAsync(
            string category, int max, CancellationToken ct)
        {
            var products = await _catalog.FindBetterQualityAsync(category, 4, max, ct);

            return products.Select(p => new AlternativeMatch(p,
                $"Calidad superior: rating {p.QualityRating}/5. Especificación: {p.Specification ?? "N/A"}"))
                .ToList();
        }

        private async Task<IReadOnlyList<AlternativeMatch>> FindOnSaleAsync(
            string category, int max, CancellationToken ct)
        {
            var products = await _catalog.FindOnSaleAsync(category, max, ct);

            return products.Select(p => new AlternativeMatch(p,
                $"En oferta: {p.SalePrice} {p.Currency} (precio normal: {p.UnitPrice}). " +
                $"Ahorro: {p.DiscountPercent}%"))
                .ToList();
        }

        private async Task<IReadOnlyList<AlternativeMatch>> FindOptimalPackAsync(
            string category, decimal? requiredQty, int max, CancellationToken ct)
        {
            if (!requiredQty.HasValue || requiredQty.Value <= 0)
            {
                return Array.Empty<AlternativeMatch>();
            }

            var products = await _catalog.FindWithPackInfoAsync(category, ct);

            return products
                .Select(p =>
                {
                    var packsNeeded = Math.Ceiling(requiredQty.Value / p.PackSize!.Value);
                    var totalCost = packsNeeded * p.PackPrice!.Value;
                    var totalQty = packsNeeded * p.PackSize.Value;
                    var waste = totalQty - requiredQty.Value;
                    var wastePct = Math.Round(waste / totalQty * 100, 1);

                    return new { Product = p, PacksNeeded = packsNeeded, TotalCost = totalCost, Waste = waste, WastePct = wastePct };
                })
                .OrderBy(x => x.TotalCost)
                .ThenBy(x => x.Waste)
                .Take(max)
                .Select(x => new AlternativeMatch(x.Product,
                    $"Presentación óptima: {x.Product.Presentation ?? $"{x.Product.PackSize} {x.Product.Unit}"}. " +
                    $"{x.PacksNeeded} packs, coste total: {x.TotalCost} {x.Product.Currency}, " +
                    $"desperdicio: {x.Waste} {x.Product.Unit} ({x.WastePct}%)"))
                .ToList();
        }

        private async Task<IReadOnlyList<AlternativeMatch>> FindAllAsync(
            string category, string description, decimal? requiredQty, int max, CancellationToken ct)
        {
            var all = new List<AlternativeMatch>();
            all.AddRange(await FindCheaperAsync(category, description, 2, ct));
            all.AddRange(await FindBetterAsync(category, 1, ct));
            all.AddRange(await FindOnSaleAsync(category, 1, ct));
            all.AddRange(await FindOptimalPackAsync(category, requiredQty, 1, ct));

            return all
                .GroupBy(a => a.Product.ItemCode)
                .Select(g => g.First())
                .Take(max)
                .ToList();
        }

        // ── Helpers ──────────────────────────────────────────────────

        private static readonly HashSet<string> _shortTermWhitelist = new(StringComparer.OrdinalIgnoreCase)
        { "pvc", "m2", "m3", "fe", "hp", "ca", "cu", "pe", "pp" };

        private static IReadOnlyList<string> ExtractSearchTerms(string text) =>
            text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t.Length > 2 || _shortTermWhitelist.Contains(t))
                .Take(7)
                .ToList();
    }
}
