using IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Erp;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using IgnakeeAI.McpServer.Supplier.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace IgnakeeAI.McpServer.Supplier.Tests
{
    public class OdooConnectorTests : IDisposable
    {
        private readonly SupplierCatalogDbContext _db;
        private readonly DataSourceSettings _config;
        private readonly ILogger<OdooConnector> _logger;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public OdooConnectorTests()
        {
            // BD en memoria aislada por test
            var options = new DbContextOptionsBuilder<SupplierCatalogDbContext>()
                .UseInMemoryDatabase(databaseName: $"OdooTest_{Guid.NewGuid()}")
                .Options;

            _db = new SupplierCatalogDbContext(options);

            _config = new DataSourceSettings
            {
                BaseUrl = "https://odoo-test.example.com",
                Database = "test_db",
                Username = "admin",
                Password = "admin123"
            };

            _logger = NullLogger<OdooConnector>.Instance;
        }

        public void Dispose()
        {
            _db.Dispose();
            GC.SuppressFinalize(this);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Responsable de crear un OdooConnector con un HttpClient que utiliza el OdooMockHttpHandler para simular respuestas de Odoo.
        /// </summary>
        /// <param name="handler">El handler que simula las respuestas de Odoo.</param>
        /// <returns>Un OdooConnector configurado con el handler simulado.</returns>
        private OdooConnector CreateConnector(OdooMockHttpHandler handler)
        {
            var httpClient = new HttpClient(handler);

            if (Uri.TryCreate(_config.BaseUrl, UriKind.Absolute, out var baseUri))
            {
                httpClient.BaseAddress = baseUri;
            }

            return new OdooConnector(
                httpClient,
                _db,
                Options.Create(_config),
                _logger);
        }

        // ── Tests: Sincronización exitosa ────────────────────────────────────────

        /// <summary>
        /// Responsable de probar que la sincronización de productos desde Odoo funciona correctamente 
        /// cuando se recibe un catálogo válido. Verifica que se importen todos los productos y que los campos se mapeen correctamente.
        /// </summary>        
        [Fact]
        public async Task SyncProductsAsync_WithValidCatalog_ImportsAllProducts()
        {
            // Arrange
            var handler = new OdooMockHttpHandler(
                OdooFakeResponses.AuthenticateSuccess(),
                OdooFakeResponses.SearchReadProducts());
            var connector = CreateConnector(handler);

            // Act
            var imported = await connector.SyncProductsAsync(_cts.Token);

            // Assert
            Assert.True(imported > 0);
            Assert.Equal(imported, _db.Products.Count());
        }

        /// <summary>
        /// Responsable de probar que los campos del producto se mapean correctamente desde la respuesta de Odoo al modelo local.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SyncProductsAsync_WithValidCatalog_MapsFieldsCorrectly()
        {
            // Arrange
            var handler = new OdooMockHttpHandler(
                OdooFakeResponses.AuthenticateSuccess(),
                OdooFakeResponses.SearchReadProducts());
            var connector = CreateConnector(handler);

            // Act
            await connector.SyncProductsAsync(_cts.Token);

            // Assert — verificar mapeo del cemento
            var cement = _db.Products.First(p => p.ItemCode == "CEM-001");
            Assert.Equal("Cemento Portland CEM II/B-L 32.5R - Saco 25 kg", cement.Description);
            Assert.Equal("cementos", cement.Category); // Many2one → lowercase
            Assert.Equal(4.85m, cement.UnitPrice);
            Assert.Equal("kg", cement.Unit);            // Many2one → nombre
            Assert.Equal(12000, cement.AvailableStock);
            Assert.Contains("cemento", cement.Keywords);
            Assert.Equal("EUR", cement.Currency);
            Assert.True(cement.IsActive);
        }

        /// <summary>
        /// Responsable de probar que las categorías de productos se mapean correctamente y se normalizan a minúsculas.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SyncProductsAsync_WithValidCatalog_MapsAllCategories()
        {
            // Arrange
            var handler = new OdooMockHttpHandler(
                OdooFakeResponses.AuthenticateSuccess(),
                OdooFakeResponses.SearchReadProducts());
            var connector = CreateConnector(handler);

            // Act
            await connector.SyncProductsAsync(_cts.Token);

            // Assert — todas las categorías deben estar en minúsculas
            var categories = _db.Products.Select(p => p.Category).Distinct().ToList();
            Assert.Contains("cementos", categories);
            Assert.Contains("aceros", categories);
            Assert.Contains("cerámicos", categories);
            Assert.Contains("pinturas", categories);
            Assert.Contains("áridos", categories);
            Assert.Contains("fontanería", categories);
            Assert.Contains("impermeabilización", categories);
            Assert.Contains("aislamientos", categories);
        }

        // ── Tests: Campos nullable (comportamiento real de Odoo) ─────────────────
        /// <summary>
        /// Responsable de probar que el conector maneja correctamente los campos que Odoo devuelve como `false` 
        /// en lugar de `null` para campos vacíos. Verifica que el conector interprete estos valores como vacíos 
        /// o predeterminados según corresponda.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SyncProductsAsync_WithNullableFields_HandlesOdooFalseValues()
        {
            // Arrange — Odoo devuelve `false` en lugar de null para campos vacíos
            var handler = new OdooMockHttpHandler(
                OdooFakeResponses.AuthenticateSuccess(),
                OdooFakeResponses.SearchReadWithNullableFields());
            var connector = CreateConnector(handler);

            // Act
            var imported = await connector.SyncProductsAsync(_cts.Token);

            // Assert — solo 1 producto importado (el otro no tiene default_code)
            Assert.Equal(1, imported);

            var product = _db.Products.Single();
            _logger.LogInformation("Producto importado: {@Product}", product.ItemCode);
            Assert.Equal("GEN-001", product.ItemCode);
            Assert.Equal("general", product.Category); // categ_id con valor → lowercase
            Assert.Equal("", product.Keywords);         // description_sale = false → ""
        }

        /// <summary>
        /// Responsable de probar que el conector omite correctamente los productos que no tienen un `default_code` (SKU) válido,
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SyncProductsAsync_SkipsProductsWithoutItemCode()
        {
            // Arrange
            var handler = new OdooMockHttpHandler(
                OdooFakeResponses.AuthenticateSuccess(),
                OdooFakeResponses.SearchReadWithNullableFields());
            var connector = CreateConnector(handler);

            // Act
            await connector.SyncProductsAsync(_cts.Token);

            // Assert — el producto con default_code = false no se importa
            Assert.DoesNotContain(_db.Products, p => p.Description == "Producto sin SKU");
        }

        // ── Tests: Catálogo vacío ────────────────────────────────────────────────

        /// <summary>
        /// Responsable de probar que el conector maneja correctamente el caso en que Odoo devuelve un catálogo vacío (sin productos).
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SyncProductsAsync_WithEmptyCatalog_ReturnsZero()
        {
            // Arrange
            var handler = new OdooMockHttpHandler(
                OdooFakeResponses.AuthenticateSuccess(),
                OdooFakeResponses.SearchReadEmpty());
            var connector = CreateConnector(handler);

            // Act
            var imported = await connector.SyncProductsAsync(_cts.Token);

            // Assert
            Assert.Equal(0, imported);
            Assert.Empty(_db.Products);
        }

        // ── Tests: Upsert (actualización de productos existentes) ────────────────
        
        /// <summary>
        /// Responsable de probar que el conector actualiza los productos existentes en lugar de duplicarlos.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SyncProductsAsync_WhenProductExists_UpdatesInsteadOfDuplicating()
        {
            // Arrange — insertar un producto existente con precio viejo
            _db.Products.Add(new Domain.Entities.CatalogProduct
            {
                ItemCode = "CEM-001",
                Description = "Cemento viejo",
                Category = "cementos",
                UnitPrice = 3.50m,
                Unit = "kg",
                Currency = "EUR"
            });
            await _db.SaveChangesAsync();

            var handler = new OdooMockHttpHandler(
                OdooFakeResponses.AuthenticateSuccess(),
                OdooFakeResponses.SearchReadProducts());
            var connector = CreateConnector(handler);

            // Act
            await connector.SyncProductsAsync(_cts.Token);

            // Assert — no debe duplicar, debe actualizar
            var cementProducts = _db.Products.Where(p => p.ItemCode == "CEM-001").ToList();
            Assert.Single(cementProducts);
            Assert.Equal(4.85m, cementProducts[0].UnitPrice); // Precio actualizado
            Assert.Equal("Cemento Portland CEM II/B-L 32.5R - Saco 25 kg", cementProducts[0].Description);
        }

        // ── Tests: Errores de autenticación ──────────────────────────────────────
        
        /// <summary>
        /// Responsable de probar que el conector lanza una excepción cuando las credenciales de Odoo son inválidas.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SyncProductsAsync_WithInvalidCredentials_ThrowsInvalidOperation()
        {
            // Arrange
            var handler = new OdooMockHttpHandler(
                OdooFakeResponses.AuthenticateInvalidCredentials(),
                OdooFakeResponses.SearchReadProducts());
            var connector = CreateConnector(handler);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => connector.SyncProductsAsync(_cts.Token));

            Assert.Contains("Credenciales de Odoo inválidas", ex.Message);
        }

        /// <summary>
        /// Responsable de probar que el conector lanza una excepción cuando el servidor de Odoo devuelve un error durante la autenticación.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SyncProductsAsync_WithServerError_ThrowsInvalidOperation()
        {
            // Arrange
            var handler = new OdooMockHttpHandler(
                OdooFakeResponses.AuthenticateServerError(),
                OdooFakeResponses.SearchReadProducts());
            var connector = CreateConnector(handler);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => connector.SyncProductsAsync(_cts.Token));

            Assert.Contains("Error de autenticación en Odoo", ex.Message);
        }

        // ── Tests: Error de lectura de productos ─────────────────────────────────
        
        /// <summary>
        /// Responsable de probar que el conector lanza una excepción cuando ocurre un error al leer los productos de Odoo.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SyncProductsAsync_WithAccessError_ThrowsInvalidOperation()
        {
            // Arrange
            var handler = new OdooMockHttpHandler(
                OdooFakeResponses.AuthenticateSuccess(),
                OdooFakeResponses.SearchReadAccessError());
            var connector = CreateConnector(handler);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => connector.SyncProductsAsync(_cts.Token));

            Assert.Contains("Error leyendo productos de Odoo", ex.Message);
        }

        // ── Tests: Comunicación HTTP ─────────────────────────────────────────────
        
        /// <summary>
        /// Responsable de probar que el conector envía dos solicitudes JSON-RPC: una para autenticación y otra para leer productos.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SyncProductsAsync_SendsTwoJsonRpcRequests()
        {
            // Arrange
            var handler = new OdooMockHttpHandler(
                OdooFakeResponses.AuthenticateSuccess(),
                OdooFakeResponses.SearchReadProducts());
            var connector = CreateConnector(handler);

            // Act
            await connector.SyncProductsAsync(_cts.Token);

            // Assert — 1 auth + 1 search_read = 2 requests
            Assert.Equal(2, handler.RequestCount);
            Assert.All(handler.RequestBodies, body => Assert.Contains("jsonrpc", body));
        }

        // ── Tests: IsAvailableAsync ──────────────────────────────────────────────
        
        /// <summary>
        /// Responsable de probar que el conector devuelve true cuando la URL de Odoo está configurada.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task IsAvailableAsync_WithConfiguredUrl_ReturnsTrue()
        {
            // Arrange
            var handler = new OdooMockHttpHandler(
                OdooFakeResponses.AuthenticateSuccess(),
                OdooFakeResponses.SearchReadEmpty());
            var connector = CreateConnector(handler);

            // Act & Assert
            Assert.True(await connector.IsAvailableAsync(_cts.Token));
        }

        /// <summary>
        /// Responsable de probar que el conector devuelve false cuando la URL de Odoo está vacía, indicando que no está disponible.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task IsAvailableAsync_WithEmptyUrl_ReturnsFalse()
        {
            // Arrange
            _config.BaseUrl = "";
            var handler = new OdooMockHttpHandler(
                OdooFakeResponses.AuthenticateSuccess(),
                OdooFakeResponses.SearchReadEmpty());
            var connector = CreateConnector(handler);

            // Act & Assert
            Assert.False(await connector.IsAvailableAsync(_cts.Token));
        }

        // ── Tests: ErpName ───────────────────────────────────────────────────────
        
        /// <summary>
        /// Responsable de probar que el conector devuelve el nombre del ERP como "Odoo".
        /// </summary>
        /// <returns></returns>
        [Fact]
        public void ErpName_ReturnsOdoo()
        {
            var handler = new OdooMockHttpHandler(
                OdooFakeResponses.AuthenticateSuccess(),
                OdooFakeResponses.SearchReadEmpty());
            var connector = CreateConnector(handler);

            Assert.Equal("Odoo", connector.ErpName);
        }

        // ── Tests: FindProductAsync ──────────────────────────────────────────────
        
        /// <summary>
        /// Responsable de probar que el conector siempre devuelve null y delega la búsqueda de productos a la base de datos local.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task FindProductAsync_AlwaysReturnsNull_DelegatesToLocalDb()
        {
            var handler = new OdooMockHttpHandler(
                OdooFakeResponses.AuthenticateSuccess(),
                OdooFakeResponses.SearchReadEmpty());
            var connector = CreateConnector(handler);

            var result = await connector.FindProductAsync("CEM-001", _cts.Token);

            Assert.Null(result);
        }

        /// <summary>
        /// Responsable de probar que el conector maneja correctamente campos numéricos
        /// cuando Odoo devuelve false en lugar de null:
        /// - list_price = false => 0
        /// - qty_available = false => null
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SyncProductsAsync_WithFalseNumericFields_MapsDefaultsWithoutFailing()
        {
            // Arrange
            var handler = new OdooMockHttpHandler(
                OdooFakeResponses.AuthenticateSuccess(),
                OdooFakeResponses.SearchReadWithFalseNumericFields());
            var connector = CreateConnector(handler);

            // Act
            var imported = await connector.SyncProductsAsync(_cts.Token);

            // Assert
            Assert.Equal(1, imported);

            var product = _db.Products.Single();
            Assert.Equal("NUM-001", product.ItemCode);
            Assert.Equal(0m, product.UnitPrice);
            Assert.Null(product.AvailableStock);
        }

    }
}
