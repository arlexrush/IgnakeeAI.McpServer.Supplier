using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Xunit;

namespace IgnakeeAI.McpServer.Supplier.Tests.Integration
{
    /// <summary>
    /// Pruebas de integración para los endpoints de administración de catálogo:
    ///   POST /admin/sync/csv
    ///   POST /admin/sync/excel
    ///   POST /admin/sync/erp  (sin conector configurado)
    ///   GET  /admin/catalog/stats
    /// </summary>
    public class AdminEndpointsIntegrationTests : IClassFixture<SupplierApiFactory>, IAsyncLifetime
    {
        private readonly SupplierApiFactory _factory;
        private readonly HttpClient _client;
        private IServiceScope? _scope;

        public AdminEndpointsIntegrationTests(SupplierApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        public async ValueTask InitializeAsync()
        {
            _scope = await _factory.SeedDatabaseAsync();
        }

        public ValueTask DisposeAsync()
        {
            _scope?.Dispose();
            return ValueTask.CompletedTask;
        }

        // ── POST /admin/sync/csv ──────────────────────────────────────────────────

        [Fact]
        public async Task POST_AdminSyncCsv_WithValidFile_Returns200AndImportsProducts()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var csvContent = BuildCsvContent(
            [
                "INT-CSV-001;Producto CSV de integración;materiales;csv,test;ud;12.50;EUR;10;120;Spec A;Caja 10 ud;100;2;https://test.local;false;;4",
                "INT-CSV-002;Segundo producto CSV;materiales;csv,test2;kg;3.80;EUR;25;90;;;500;1;;false;;3"
            ]);

            using var form = BuildCsvMultipartForm(csvContent, "catalogo_test.csv");

            // Act
            var response = await _client.PostAsync("/admin/sync/csv", form, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("csv", root.GetProperty("source").GetString());
            Assert.Equal(2, root.GetProperty("productsImported").GetInt32());
        }

        [Fact]
        public async Task POST_AdminSyncCsv_WithUpsert_UpdatesExistingProduct()
        {
            // Arrange — el catálogo ya tiene CEM-STD; el CSV lo actualiza con precio nuevo
            var ct = TestContext.Current.CancellationToken;
            var csvContent = BuildCsvContent(
            [
                "CEM-STD;Cemento estándar actualizado;cementos;cemento;kg;7.99;EUR;20;150;;;5000;1;;false;;3"
            ]);

            using var form = BuildCsvMultipartForm(csvContent, "update.csv");

            // Act
            var response = await _client.PostAsync("/admin/sync/csv", form, ct);

            // Assert HTTP
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Assert BD — verificar que el precio se actualizó
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SupplierCatalogDbContext>();
            var product = db.Products.SingleOrDefault(p => p.ItemCode == "CEM-STD");

            Assert.NotNull(product);
            Assert.Equal(7.99m, product.UnitPrice);
            Assert.Equal("Cemento estándar actualizado", product.Description);
        }

        [Fact]
        public async Task POST_AdminSyncCsv_WithoutFile_Returns400()
        {
            // Arrange — enviar form vacío sin archivo
            var ct = TestContext.Current.CancellationToken;
            using var form = new MultipartFormDataContent();

            // Act
            var response = await _client.PostAsync("/admin/sync/csv", form, ct);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task POST_AdminSyncCsv_WithEmptyFile_ReturnsZeroImported()
        {
            // Arrange — CSV solo con cabeceras, sin filas de datos
            var ct = TestContext.Current.CancellationToken;
            var csvContent = BuildCsvContent([]);
            using var form = BuildCsvMultipartForm(csvContent, "empty.csv");

            // Act
            var response = await _client.PostAsync("/admin/sync/csv", form, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(0, doc.RootElement.GetProperty("productsImported").GetInt32());
        }

        // ── POST /admin/sync/excel ────────────────────────────────────────────────

        [Fact]
        public async Task POST_AdminSyncExcel_WithValidFile_Returns200AndImportsProducts()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var excelBytes = ExcelTestBuilder.Build(
            [
                new ExcelProductRow("INT-XLS-001", "Producto Excel integración", "materiales",
                    "excel,test", "ud", 15.00m, "EUR", 10m, 140m,
                    "Spec B", "Caja 10 ud", 200, 3, "https://test.local", false, null, 4),
                new ExcelProductRow("INT-XLS-002", "Segundo Excel", "materiales",
                    "excel,test2", "kg", 4.20m, "EUR", 25m, 100m,
                    null, null, 750, 2, null, false, null, 3)
            ]);

            using var form = BuildExcelMultipartForm(excelBytes, "catalogo_test.xlsx");

            // Act
            var response = await _client.PostAsync("/admin/sync/excel", form, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("excel", root.GetProperty("source").GetString());
            Assert.Equal(2, root.GetProperty("productsImported").GetInt32());
        }

        [Fact]
        public async Task POST_AdminSyncExcel_WithUpsert_UpdatesExistingProduct()
        {
            // Arrange — actualizar ACE-001 con precio nuevo desde Excel
            var ct = TestContext.Current.CancellationToken;
            var excelBytes = ExcelTestBuilder.Build(
            [
                new ExcelProductRow("ACE-001", "Acero corrugado actualizado", "aceros",
                    "acero,corrugado", "m", 9.99m, "EUR", 12m, 115m,
                    null, null, 2500, 2, null, false, null, 4)
            ]);

            using var form = BuildExcelMultipartForm(excelBytes, "update.xlsx");

            // Act
            var response = await _client.PostAsync("/admin/sync/excel", form, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Assert BD
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SupplierCatalogDbContext>();
            var product = db.Products.SingleOrDefault(p => p.ItemCode == "ACE-001");

            Assert.NotNull(product);
            Assert.Equal(9.99m, product.UnitPrice);
        }

        [Fact]
        public async Task POST_AdminSyncExcel_WithOnSaleProduct_PersistsSalePrice()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var excelBytes = ExcelTestBuilder.Build(
            [
                new ExcelProductRow("PROMO-XLS-001", "Producto en oferta Excel", "materiales",
                    "oferta,promo", "ud", 20.00m, "EUR", 5m, 90m,
                    null, null, 300, 1, null, true, 14.99m, 4)
            ]);

            using var form = BuildExcelMultipartForm(excelBytes, "promo.xlsx");

            // Act
            var response = await _client.PostAsync("/admin/sync/excel", form, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            // Assert BD
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SupplierCatalogDbContext>();
            var product = db.Products.SingleOrDefault(p => p.ItemCode == "PROMO-XLS-001");

            Assert.NotNull(product);
            Assert.True(product.IsOnSale);
            Assert.Equal(14.99m, product.SalePrice);
        }

        [Fact]
        public async Task POST_AdminSyncExcel_WithoutFile_Returns400()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            using var form = new MultipartFormDataContent();

            // Act
            var response = await _client.PostAsync("/admin/sync/excel", form, ct);
            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        // ── POST /admin/sync/erp ──────────────────────────────────────────────────

        [Fact]
        public async Task POST_AdminSyncErp_WithNoErpConfigured_Returns400()
        {
            var ct = TestContext.Current.CancellationToken;
            // Act — la factory no configura ningún ERP (Erp:Provider = "")
            var response = await _client.PostAsync("/admin/sync/erp", null, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains("No hay conector ERP configurado", doc.RootElement.GetProperty("error").GetString());
        }

        // ── GET /admin/catalog/stats ──────────────────────────────────────────────

        [Fact]
        public async Task GET_AdminCatalogStats_Returns200WithStats()
        {
            // Act
            var ct = TestContext.Current.CancellationToken;
            var response = await _client.GetAsync("/admin/catalog/stats", ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(root.GetProperty("totalProducts").GetInt32() >= 4);
            Assert.True(root.GetProperty("productsOnSale").GetInt32() >= 1);
        }

        [Fact]
        public async Task GET_AdminCatalogStats_ReturnsCorrectCategoryDistribution()
        {
            // Act
            var ct = TestContext.Current.CancellationToken;
            var response = await _client.GetAsync("/admin/catalog/stats", ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);

            var categories = doc.RootElement
                .GetProperty("categories") // array de { category: string, count: int }, 
                .EnumerateArray() // convertir a diccionario para aserciones más fáciles
                .ToDictionary(
                    c => c.GetProperty("category").GetString()!,
                    c => c.GetProperty("count").GetInt32()); // { "cementos": 15, "aceros": 3, ... }

            // Assert — el catálogo sembrado tiene 3 cementos y 1 acero
            Assert.True(categories.ContainsKey("cementos"));
            Assert.Equal(14, categories["cementos"]);
            Assert.True(categories.ContainsKey("aceros"));
            Assert.Equal(3, categories["aceros"]);
        }

        [Fact]
        public async Task GET_AdminCatalogStats_OnSaleCount_MatchesSeededData()
        {
            // Act
            var ct = TestContext.Current.CancellationToken;
            var response = await _client.GetAsync("/admin/catalog/stats", ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);

            // Assert — solo CEM-OFFER está en oferta en el catálogo sembrado
            Assert.Equal(26, doc.RootElement.GetProperty("productsOnSale").GetInt32());
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static string BuildCsvContent(IEnumerable<string> dataRows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("ItemCode;Description;Category;Keywords;Unit;UnitPrice;Currency;PackSize;PackPrice;Specification;Presentation;AvailableStock;LeadTimeDays;ProductUrl;IsOnSale;SalePrice;QualityRating");

            foreach (var row in dataRows)
                sb.AppendLine(row);

            return sb.ToString();
        }

        private static MultipartFormDataContent BuildCsvMultipartForm(string csvContent, string fileName)
        {
            var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            form.Add(fileContent, "file", fileName);
            return form;
        }

        private static MultipartFormDataContent BuildExcelMultipartForm(byte[] excelBytes, string fileName)
        {
            var form = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(excelBytes);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
            form.Add(fileContent, "file", fileName);
            return form;
        }
    }
}
