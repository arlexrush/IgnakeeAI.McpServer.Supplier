using IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Erp;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using IgnakeeAI.McpServer.Supplier.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace IgnakeeAI.McpServer.Supplier.Tests
{
    public class SapConnectorTests : IDisposable
    {
        private readonly SupplierCatalogDbContext _db;
        private readonly DataSourceSettings _config;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public SapConnectorTests()
        {
            var options = new DbContextOptionsBuilder<SupplierCatalogDbContext>()
                .UseInMemoryDatabase(databaseName: $"SapTest_{Guid.NewGuid()}")
                .Options;

            _db = new SupplierCatalogDbContext(options);

            _config = new DataSourceSettings
            {
                BaseUrl = "https://sap-test.example.com/b1s/v1",
                Database = "TEST_COMPANY",
                Username = "manager",
                Password = "secret"
            };
        }

        public void Dispose()
        {
            _cts.Dispose();
            _db.Dispose();
            GC.SuppressFinalize(this);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private SapConnector CreateConnector(SapMockHttpHandler handler)
        {
            var httpClient = new HttpClient(handler);

            if (Uri.TryCreate(_config.BaseUrl, UriKind.Absolute, out var baseUri))
                httpClient.BaseAddress = baseUri;

            return new SapConnector(
                httpClient,
                _db,
                Options.Create(_config),
                NullLogger<SapConnector>.Instance);
        }

        // ── Tests: Sincronización exitosa ────────────────────────────────────────

        /// <summary>
        /// Verifica que la sincronización con un catálogo SAP válido importa todos los productos.
        /// </summary>
        [Fact]
        public async Task SyncProductsAsync_WithValidCatalog_ImportsAllProducts()
        {
            // Arrange
            var handler = new SapMockHttpHandler(
                SapFakeResponses.LoginSuccess(),
                SapFakeResponses.ItemsPage());
            var connector = CreateConnector(handler);

            // Act
            var imported = await connector.SyncProductsAsync(_cts.Token);

            // Assert
            Assert.Equal(3, imported);
            Assert.Equal(3, _db.Products.Count());
        }

        /// <summary>
        /// Verifica que los campos del producto se mapean correctamente desde la respuesta SAP OData.
        /// </summary>
        [Fact]
        public async Task SyncProductsAsync_WithValidCatalog_MapsFieldsCorrectly()
        {
            // Arrange
            var handler = new SapMockHttpHandler(
                SapFakeResponses.LoginSuccess(),
                SapFakeResponses.ItemsPage());
            var connector = CreateConnector(handler);

            // Act
            await connector.SyncProductsAsync(_cts.Token);

            // Assert
            var cement = _db.Products.First(p => p.ItemCode == "SAP-CEM-001");
            Assert.Equal("Cemento Portland SAP CEM II", cement.Description);
            Assert.Equal("sap-group-10", cement.Category);
            Assert.Equal(4.85m, cement.UnitPrice);
            Assert.Equal("KG", cement.Unit);
            Assert.Equal(10000, cement.AvailableStock);
            Assert.Equal("EUR", cement.Currency);
            Assert.True(cement.IsActive);
        }

        /// <summary>
        /// Verifica que todas las categorías SAP se formatean como "sap-group-{n}".
        /// </summary>
        [Fact]
        public async Task SyncProductsAsync_WithValidCatalog_MapsGroupCodesAsCategories()
        {
            // Arrange
            var handler = new SapMockHttpHandler(
                SapFakeResponses.LoginSuccess(),
                SapFakeResponses.ItemsPage());
            var connector = CreateConnector(handler);

            // Act
            await connector.SyncProductsAsync(_cts.Token);

            // Assert
            var categories = _db.Products.Select(p => p.Category).Distinct().ToList();
            Assert.Contains("sap-group-10", categories);
            Assert.Contains("sap-group-20", categories);
            Assert.Contains("sap-group-30", categories);
        }

        // ── Tests: Catálogo vacío ────────────────────────────────────────────────

        /// <summary>
        /// Verifica que un catálogo SAP vacío devuelve 0 y no persiste productos.
        /// </summary>
        [Fact]
        public async Task SyncProductsAsync_WithEmptyCatalog_ReturnsZero()
        {
            // Arrange
            var handler = new SapMockHttpHandler(
                SapFakeResponses.LoginSuccess(),
                SapFakeResponses.ItemsEmpty());
            var connector = CreateConnector(handler);

            // Act
            var imported = await connector.SyncProductsAsync(_cts.Token);

            // Assert
            Assert.Equal(0, imported);
            Assert.Empty(_db.Products);
        }

        // ── Tests: Upsert ────────────────────────────────────────────────────────

        /// <summary>
        /// Verifica que un producto existente se actualiza en lugar de duplicarse.
        /// </summary>
        [Fact]
        public async Task SyncProductsAsync_WhenProductExists_UpdatesInsteadOfDuplicating()
        {
            // Arrange — insertar producto con precio antiguo
            _db.Products.Add(new Domain.Entities.CatalogProduct
            {
                ItemCode = "SAP-CEM-001",
                Description = "Descripción antigua",
                Category = "sap-group-10",
                UnitPrice = 1.00m,
                Unit = "KG",
                Currency = "EUR"
            });
            await _db.SaveChangesAsync();

            var handler = new SapMockHttpHandler(
                SapFakeResponses.LoginSuccess(),
                SapFakeResponses.ItemsPage());
            var connector = CreateConnector(handler);

            // Act
            await connector.SyncProductsAsync(_cts.Token);

            // Assert — no duplicar; actualizar precio
            var results = _db.Products.Where(p => p.ItemCode == "SAP-CEM-001").ToList();
            Assert.Single(results);
            Assert.Equal(4.85m, results[0].UnitPrice);
            Assert.Equal("Cemento Portland SAP CEM II", results[0].Description);
        }

        // ── Tests: Campos nullable ────────────────────────────────────────────────

        /// <summary>
        /// Verifica que los campos opcionales nulos/vacíos se mapean con valores por defecto.
        /// </summary>
        [Fact]
        public async Task SyncProductsAsync_WithNullableFields_MapsDefaultsCorrectly()
        {
            // Arrange
            var handler = new SapMockHttpHandler(
                SapFakeResponses.LoginSuccess(),
                SapFakeResponses.ItemsWithNullableFields());
            var connector = CreateConnector(handler);

            // Act
            var imported = await connector.SyncProductsAsync(_cts.Token);

            // Assert
            Assert.Equal(1, imported);
            var product = _db.Products.Single();
            Assert.Equal("SAP-NULL-001", product.ItemCode);
            Assert.Equal(0m, product.UnitPrice);
            Assert.Equal("ud", product.Unit);   // null → "ud"
            Assert.Null(product.AvailableStock); // null → null
        }

        // ── Tests: Comunicación HTTP ─────────────────────────────────────────────

        /// <summary>
        /// Verifica que el conector envía Login, Items y Logout en ese orden.
        /// </summary>
        [Fact]
        public async Task SyncProductsAsync_SendsLoginItemsAndLogoutRequests()
        {
            // Arrange
            var handler = new SapMockHttpHandler(
                SapFakeResponses.LoginSuccess(),
                SapFakeResponses.ItemsPage());
            var connector = CreateConnector(handler);

            // Act
            await connector.SyncProductsAsync(_cts.Token);

            // Assert — al menos Login + Items + Logout = 3 requests
            Assert.True(handler.RequestCount >= 3);
            Assert.Contains(handler.RequestUrls, u => u.Contains("Login"));
            Assert.Contains(handler.RequestUrls, u => u.Contains("Items"));
            Assert.Contains(handler.RequestUrls, u => u.Contains("Logout"));
        }

        // ── Tests: IsAvailableAsync ──────────────────────────────────────────────

        /// <summary>
        /// Verifica que IsAvailableAsync devuelve true cuando la URL está configurada.
        /// </summary>
        [Fact]
        public async Task IsAvailableAsync_WithConfiguredUrl_ReturnsTrue()
        {
            // Arrange
            var handler = new SapMockHttpHandler(
                SapFakeResponses.LoginSuccess(),
                SapFakeResponses.ItemsEmpty());
            var connector = CreateConnector(handler);

            // Act & Assert
            Assert.True(await connector.IsAvailableAsync(_cts.Token));
        }

        /// <summary>
        /// Verifica que IsAvailableAsync devuelve false cuando la URL está vacía.
        /// </summary>
        [Fact]
        public async Task IsAvailableAsync_WithEmptyUrl_ReturnsFalse()
        {
            // Arrange
            _config.BaseUrl = "";
            var handler = new SapMockHttpHandler(
                SapFakeResponses.LoginSuccess(),
                SapFakeResponses.ItemsEmpty());
            var connector = CreateConnector(handler);

            // Act & Assert
            Assert.False(await connector.IsAvailableAsync(_cts.Token));
        }

        // ── Tests: ErpName ───────────────────────────────────────────────────────

        [Fact]
        public void ErpName_ReturnsSap()
        {
            // Arrange
            var handler = new SapMockHttpHandler(
                SapFakeResponses.LoginSuccess(),
                SapFakeResponses.ItemsEmpty());
            var connector = CreateConnector(handler);

            // Assert
            Assert.Equal("SAP", connector.ErpName);
        }

        // ── Tests: FindProductAsync ──────────────────────────────────────────────

        /// <summary>
        /// Verifica que FindProductAsync siempre devuelve null y delega a la BD local.
        /// </summary>
        [Fact]
        public async Task FindProductAsync_AlwaysReturnsNull()
        {
            // Arrange
            var handler = new SapMockHttpHandler(
                SapFakeResponses.LoginSuccess(),
                SapFakeResponses.ItemsEmpty());
            var connector = CreateConnector(handler);

            // Act
            var result = await connector.FindProductAsync("SAP-CEM-001", _cts.Token);

            // Assert
            Assert.Null(result);
        }
    }
}
