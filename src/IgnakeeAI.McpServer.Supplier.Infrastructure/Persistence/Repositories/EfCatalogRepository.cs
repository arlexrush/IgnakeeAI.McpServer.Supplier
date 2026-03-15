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
            var query = _db.Products.Where(p => p.IsActive);
            foreach (var term in searchTerms)
            {
                var t = term;
                query = query.Where(p =>
                    EF.Functions.Like(p.Description, $"%{t}%") ||
                    EF.Functions.Like(p.Keywords, $"%{t}%"));
            }

            if (_db.Database.IsSqlite())
            {
                var products = await query.ToListAsync(ct);
                return products
                    .OrderBy(p => p.UnitPrice)
                    .FirstOrDefault();
            }

            return await query.OrderBy(p => p.UnitPrice).FirstOrDefaultAsync(ct);
        }

        public async Task<string?> InferCategoryAsync(
            IReadOnlyList<string> searchTerms, CancellationToken ct)
        {
            var query = _db.Products.Where(p => p.IsActive);
            foreach (var term in searchTerms)
            {
                var t = term;
                query = query.Where(p =>
                    EF.Functions.Like(p.Description, $"%{t}%") ||
                    EF.Functions.Like(p.Keywords, $"%{t}%"));
            }
            var product = await query.FirstOrDefaultAsync(ct);
            return product?.Category;
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
