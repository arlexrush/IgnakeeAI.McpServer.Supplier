using ClosedXML.Excel;
using IgnakeeAI.McpServer.Supplier.Domain.Entities;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors
{
    /// <summary>
    /// Importa productos desde un archivo Excel (.xlsx) a la base de datos.
    ///
    /// USO: El proveedor puede mantener su catálogo en Excel y ejecutar una importación
    /// periódica (manual o programada) para sincronizar los datos con el servidor MCP.
    ///
    /// FORMATO ESPERADO DEL EXCEL (hoja "Catalogo"):
    ///   A: ItemCode | B: Description | C: Category | D: Keywords | E: Unit
    ///   F: UnitPrice | G: Currency | H: PackSize | I: PackPrice | J: Specification
    ///   K: Presentation | L: AvailableStock | M: LeadTimeDays | N: ProductUrl
    ///   O: IsOnSale | P: SalePrice | Q: QualityRating
    ///
    /// Primera fila = cabeceras (se omite).
    /// </summary>
    public class ExcelCatalogConnector
    {
        private readonly SupplierCatalogDbContext _db;
        private readonly ILogger<ExcelCatalogConnector> _logger;

        public ExcelCatalogConnector(SupplierCatalogDbContext db, ILogger<ExcelCatalogConnector> logger)
        {
            _db = db;
            _logger = logger;
        }

        /// <summary>
        /// Importa productos desde un archivo Excel. Hace upsert por ItemCode.
        /// </summary>
        public async Task<int> ImportAsync(string filePath, CancellationToken ct = default)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Archivo Excel no encontrado: {filePath}");

            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheets.FirstOrDefault(ws =>
                ws.Name.Equals("Catalogo", StringComparison.OrdinalIgnoreCase))
                ?? workbook.Worksheets.First();

            var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1) // Saltar cabeceras
                ?? Enumerable.Empty<IXLRangeRow>();

            var imported = 0;
            foreach (var row in rows)
            {
                try
                {
                    var itemCode = row.Cell(1).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(itemCode)) continue;

                    // Buscar existente o crear nuevo
                    var existing = _db.Products.FirstOrDefault(p => p.ItemCode == itemCode);
                    var product = existing ?? new CatalogProduct();

                    product.ItemCode = itemCode;
                    product.Description = row.Cell(2).GetString().Trim();
                    product.Category = row.Cell(3).GetString().Trim().ToLowerInvariant();
                    product.Keywords = row.Cell(4).GetString().Trim();
                    product.Unit = row.Cell(5).GetString().Trim();
                    product.UnitPrice = (decimal)row.Cell(6).GetDouble();
                    product.Currency = row.Cell(7).GetString().Trim().ToUpperInvariant();
                    if (string.IsNullOrWhiteSpace(product.Currency)) product.Currency = "EUR";
                    product.PackSize = row.Cell(8).IsEmpty() ? null : (decimal)row.Cell(8).GetDouble();
                    product.PackPrice = row.Cell(9).IsEmpty() ? null : (decimal)row.Cell(9).GetDouble();
                    product.Specification = row.Cell(10).GetString().Trim();
                    product.Presentation = row.Cell(11).GetString().Trim();
                    product.AvailableStock = row.Cell(12).IsEmpty() ? null : (int)row.Cell(12).GetDouble();
                    product.LeadTimeDays = row.Cell(13).IsEmpty() ? null : (int)row.Cell(13).GetDouble();
                    product.ProductUrl = row.Cell(14).GetString().Trim();
                    product.IsOnSale = !row.Cell(15).IsEmpty() && row.Cell(15).GetBoolean();
                    product.SalePrice = row.Cell(16).IsEmpty() ? null : (decimal)row.Cell(16).GetDouble();
                    product.QualityRating = row.Cell(17).IsEmpty() ? null : (int)row.Cell(17).GetDouble();
                    product.UpdatedAt = DateTime.UtcNow;
                    product.IsActive = true;

                    if (existing is null)
                        _db.Products.Add(product);

                    imported++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error importando fila {Row} del Excel.", row.RowNumber());
                }
            }

            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Importación Excel completada: {Count} productos importados desde {File}.", imported, filePath);
            return imported;
        }
    }
}
