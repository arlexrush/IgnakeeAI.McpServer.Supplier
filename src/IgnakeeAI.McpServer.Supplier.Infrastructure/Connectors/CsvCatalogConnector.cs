using CsvHelper;
using CsvHelper.Configuration;
using IgnakeeAI.McpServer.Supplier.Domain.Entities;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Models;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors
{
    /// <summary>
    /// Importa productos desde un archivo CSV al catálogo.
    ///
    /// FORMATO ESPERADO (separador: punto y coma para compatibilidad europea):
    ///   ItemCode;Description;Category;Keywords;Unit;UnitPrice;Currency;PackSize;PackPrice;
    ///   Specification;Presentation;AvailableStock;LeadTimeDays;ProductUrl;IsOnSale;SalePrice;QualityRating
    /// </summary>
    public class CsvCatalogConnector
    {
        private readonly SupplierCatalogDbContext _db;
        private readonly ILogger<CsvCatalogConnector> _logger;

        public CsvCatalogConnector(SupplierCatalogDbContext db, ILogger<CsvCatalogConnector> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<int> ImportAsync(string filePath, CancellationToken ct = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Archivo CSV no encontrado: {filePath}");

            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = ";",
                HasHeaderRecord = true,
                MissingFieldFound = null,
                BadDataFound = null
            };

            using var reader = new StreamReader(filePath);
            using var csv = new CsvReader(reader, config);
            var records = csv.GetRecords<CsvProductRow>().ToList();

            var imported = 0;
            foreach (var row in records)
            {
                var existing = _db.Products.FirstOrDefault(p => p.ItemCode == row.ItemCode);
                var product = existing ?? new CatalogProduct();

                product.ItemCode = row.ItemCode.Trim();
                product.Description = row.Description?.Trim() ?? "";
                product.Category = row.Category?.Trim().ToLowerInvariant() ?? "";
                product.Keywords = row.Keywords?.Trim() ?? "";
                product.Unit = row.Unit?.Trim() ?? "ud";
                product.UnitPrice = row.UnitPrice;
                product.Currency = string.IsNullOrWhiteSpace(row.Currency) ? "EUR" : row.Currency.Trim().ToUpperInvariant();
                product.PackSize = row.PackSize;
                product.PackPrice = row.PackPrice;
                product.Specification = row.Specification?.Trim();
                product.Presentation = row.Presentation?.Trim();
                product.AvailableStock = row.AvailableStock;
                product.LeadTimeDays = row.LeadTimeDays;
                product.ProductUrl = row.ProductUrl?.Trim();
                product.IsOnSale = row.IsOnSale;
                product.SalePrice = row.SalePrice;
                product.QualityRating = row.QualityRating;
                product.UpdatedAt = DateTime.UtcNow;
                product.IsActive = true;

                if (!CatalogProductImportValidator.TryValidate(product, out var rejectionReason))
                {
                    _logger.LogWarning("Producto CSV rechazado: {Reason}.", rejectionReason);
                    continue;
                }

                if (existing is null)
                    _db.Products.Add(product);

                imported++;
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Importación CSV completada: {Count} productos.", imported);
            return imported;
        }
        
    }
}
