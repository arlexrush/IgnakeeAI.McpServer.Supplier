using IgnakeeAI.McpServer.Supplier.Application.Interfaces;
using IgnakeeAI.McpServer.Supplier.Application.Models;
using IgnakeeAI.McpServer.Supplier.Domain.Entities;
using IgnakeeAI.McpServer.Supplier.Domain.Enums;

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

        public CatalogSearchService(ICatalogRepository catalog, ISupplierConfig supplierConfig)
        {
            _catalog = catalog;
            _supplierConfig = supplierConfig;
        }

        /// <summary>Busca un producto por código o descripción y devuelve el resultado de precio.</summary>
        public async Task<PriceResult> GetPriceAsync(
            string itemDescription, string? itemCode, string currency, CancellationToken ct)
        {
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
                return new PriceResult(Found: false);
            }

            return new PriceResult(
                Found: true,
                ItemCode: product.ItemCode,
                Description: product.Description,
                UnitPrice: product.EffectivePrice,
                Currency: product.Currency,
                Unit: product.Unit,
                PackSize: product.PackSize,
                PackPrice: product.PackPrice,
                Specification: product.Specification,
                Presentation: product.Presentation,
                IsOnSale: product.IsOnSale,
                OriginalPrice: product.IsOnSale ? product.UnitPrice : null,
                QualityRating: product.QualityRating,
                Url: product.ProductUrl,
                ValidUntil: product.ValidUntil,
                UpdatedAt: product.UpdatedAt,
                ContactEmail: _supplierConfig.ContactEmail,
                ContactPhone: _supplierConfig.ContactPhone,
                ContactAddress: _supplierConfig.ContactAddress);
        }

        /// <summary>Busca alternativas según criterio de sustitución.</summary>
        public async Task<IReadOnlyList<AlternativeMatch>> SearchAlternativesAsync(
            string itemDescription, string? category, SubstitutionCriteria criteria,
            decimal? requiredQuantity, int maxResults, CancellationToken ct)
        {
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

        /// <summary>Consulta disponibilidad por código.</summary>
        public async Task<AvailabilityResult> CheckAvailabilityAsync(string itemCode, CancellationToken ct)
        {
            var product = await _catalog.FindByCodeAsync(itemCode, ct);
            if (product is null)
            {
                return new AvailabilityResult(Found: false);
            }

            return new AvailabilityResult(
                Found: true,
                AvailableStock: product.AvailableStock ?? 0,
                LeadTimeDays: product.LeadTimeDays ?? 0);
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

        private static IReadOnlyList<string> ExtractSearchTerms(string text) =>
            text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(t => t.Length > 2)
                .Take(5)
                .ToList();
    }
}
