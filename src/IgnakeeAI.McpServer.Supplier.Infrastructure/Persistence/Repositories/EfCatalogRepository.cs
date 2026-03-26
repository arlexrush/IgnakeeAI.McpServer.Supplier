using IgnakeeAI.McpServer.Supplier.Application.Interfaces;
using IgnakeeAI.McpServer.Supplier.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Implementación de ICatalogRepository con EF Core.
    /// Funciona con cualquier provider configurado (SQLite, PostgreSQL, SQL Server, MySQL).
    /// </summary>
    public class EfCatalogRepository : ICatalogRepository
    {
        private readonly SupplierCatalogDbContext _db;

        public EfCatalogRepository(SupplierCatalogDbContext db) => _db = db;

        public async Task<CatalogProduct?> FindByCodeAsync(string itemCode, CancellationToken ct) =>
            await _db.Products
                .Where(p => p.IsActive && p.ItemCode == itemCode)
                .FirstOrDefaultAsync(ct);

        public async Task<CatalogProduct?> FindByDescriptionAsync(
            IReadOnlyList<string> searchTerms, CancellationToken ct)
        {
            var allProducts = await _db.Products
        .Where(p => p.IsActive)
        .ToListAsync(ct);

            // Scoring: cuántos términos coinciden
            return allProducts
                .Select(p => new {
                    Product = p,
                    Score = searchTerms.Count(t =>
                        p.Description.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        (p.Keywords?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false))
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .ThenBy(x => x.Product.UnitPrice)
                .FirstOrDefault()?.Product;
        }

        public async Task<string?> InferCategoryAsync(
            IReadOnlyList<string> searchTerms, CancellationToken ct)
        {
            var allProducts = await _db.Products.Where(p => p.IsActive).ToListAsync(ct);
            return allProducts
                .Select(p => new {
                    p.Category,
                    Score = searchTerms.Count(t =>
                        p.Description.Contains(t, StringComparison.OrdinalIgnoreCase) ||
                        (p.Keywords?.Contains(t, StringComparison.OrdinalIgnoreCase) ?? false))
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .FirstOrDefault()?.Category;
        }

        public async Task<IReadOnlyList<CatalogProduct>> FindCheaperInCategoryAsync(
            string category, decimal referencePrice, int max, CancellationToken ct)
        {
            var query = _db.Products
                .Where(p => p.IsActive && p.Category == category && p.UnitPrice < referencePrice);

            if (_db.Database.IsSqlite())
            {
                var products = await query.ToListAsync(ct);
                return products
                    .OrderBy(p => p.UnitPrice)
                    .Take(max)
                    .ToList();
            }

            return await query
                .OrderBy(p => p.UnitPrice)
                .Take(max)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<CatalogProduct>> FindBetterQualityAsync(
            string category, int minRating, int max, CancellationToken ct)
        {
            var query = _db.Products
                .Where(p => p.IsActive && p.Category == category && p.QualityRating >= minRating);

            if (_db.Database.IsSqlite())
            {
                var products = await query.ToListAsync(ct);
                return products
                    .OrderByDescending(p => p.QualityRating)
                    .ThenBy(p => p.UnitPrice)
                    .Take(max)
                    .ToList();
            }

            return await query
                .OrderByDescending(p => p.QualityRating)
                .ThenBy(p => p.UnitPrice)
                .Take(max)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<CatalogProduct>> FindOnSaleAsync(
            string category, int max, CancellationToken ct)
        {
            var query = _db.Products
                .Where(p => p.IsActive && p.Category == category && p.IsOnSale && p.SalePrice.HasValue);

            if (_db.Database.IsSqlite())
            {
                var products = await query.ToListAsync(ct);
                return products
                    .OrderBy(p => p.SalePrice)
                    .Take(max)
                    .ToList();
            }

            return await query
                .OrderBy(p => p.SalePrice)
                .Take(max)
                .ToListAsync(ct);
        }

        public async Task<IReadOnlyList<CatalogProduct>> FindWithPackInfoAsync(
            string category, CancellationToken ct) =>
            await _db.Products
                .Where(p => p.IsActive && p.Category == category && p.PackSize > 0 && p.PackPrice > 0)
                .ToListAsync(ct);
    }
}
