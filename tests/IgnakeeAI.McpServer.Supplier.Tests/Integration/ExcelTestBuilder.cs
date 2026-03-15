using ClosedXML.Excel;

namespace IgnakeeAI.McpServer.Supplier.Tests.Integration
{
    /// <summary>
    /// Genera archivos Excel (.xlsx) en memoria para usar en pruebas de integración.
    /// Produce la estructura de columnas A–Q que espera ExcelCatalogConnector.
    /// </summary>
    public static class ExcelTestBuilder
    {
        public static byte[] Build(IEnumerable<ExcelProductRow> rows)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Catalogo");

            // Cabeceras (fila 1)
            ws.Cell(1, 1).Value = "ItemCode";
            ws.Cell(1, 2).Value = "Description";
            ws.Cell(1, 3).Value = "Category";
            ws.Cell(1, 4).Value = "Keywords";
            ws.Cell(1, 5).Value = "Unit";
            ws.Cell(1, 6).Value = "UnitPrice";
            ws.Cell(1, 7).Value = "Currency";
            ws.Cell(1, 8).Value = "PackSize";
            ws.Cell(1, 9).Value = "PackPrice";
            ws.Cell(1, 10).Value = "Specification";
            ws.Cell(1, 11).Value = "Presentation";
            ws.Cell(1, 12).Value = "AvailableStock";
            ws.Cell(1, 13).Value = "LeadTimeDays";
            ws.Cell(1, 14).Value = "ProductUrl";
            ws.Cell(1, 15).Value = "IsOnSale";
            ws.Cell(1, 16).Value = "SalePrice";
            ws.Cell(1, 17).Value = "QualityRating";

            var rowIdx = 2;
            foreach (var row in rows)
            {
                ws.Cell(rowIdx, 1).Value = row.ItemCode;
                ws.Cell(rowIdx, 2).Value = row.Description;
                ws.Cell(rowIdx, 3).Value = row.Category;
                ws.Cell(rowIdx, 4).Value = row.Keywords;
                ws.Cell(rowIdx, 5).Value = row.Unit;
                ws.Cell(rowIdx, 6).Value = (double)row.UnitPrice;
                ws.Cell(rowIdx, 7).Value = row.Currency;

                if (row.PackSize.HasValue)
                    ws.Cell(rowIdx, 8).Value = (double)row.PackSize.Value;

                if (row.PackPrice.HasValue)
                    ws.Cell(rowIdx, 9).Value = (double)row.PackPrice.Value;

                if (row.Specification is not null)
                    ws.Cell(rowIdx, 10).Value = row.Specification;

                if (row.Presentation is not null)
                    ws.Cell(rowIdx, 11).Value = row.Presentation;

                if (row.AvailableStock.HasValue)
                    ws.Cell(rowIdx, 12).Value = row.AvailableStock.Value;

                if (row.LeadTimeDays.HasValue)
                    ws.Cell(rowIdx, 13).Value = row.LeadTimeDays.Value;

                if (row.ProductUrl is not null)
                    ws.Cell(rowIdx, 14).Value = row.ProductUrl;

                ws.Cell(rowIdx, 15).Value = row.IsOnSale;

                if (row.SalePrice.HasValue)
                    ws.Cell(rowIdx, 16).Value = (double)row.SalePrice.Value;

                if (row.QualityRating.HasValue)
                    ws.Cell(rowIdx, 17).Value = row.QualityRating.Value;

                rowIdx++;
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }
    }
    
}
