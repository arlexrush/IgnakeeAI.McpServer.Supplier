using IgnakeeAI.McpServer.Supplier.Application.Services;
using IgnakeeAI.McpServer.Supplier.Domain.Entities;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence.Repositories;
using IgnakeeAI.McpServer.Supplier.McpTools;
using IgnakeeAI.McpServer.Supplier.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace IgnakeeAI.McpServer.Supplier.Tests
{
    public class PricingToolsTests : IDisposable
    {
        private readonly SupplierCatalogDbContext _db;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public PricingToolsTests()
        {
            var options = new DbContextOptionsBuilder<SupplierCatalogDbContext>()
                .UseInMemoryDatabase(databaseName: $"PricingToolsTest_{Guid.NewGuid()}")
                .Options;

            _db = new SupplierCatalogDbContext(options);
            SeedCatalog();
        }

        public void Dispose()
        {
            _cts.Dispose();
            _db.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task GetPrice_WithItemCode_ReturnsExpectedProduct()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.GetPrice(itemDescription: "", itemCode: "CEM-STD");
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.True(root.GetProperty("found").GetBoolean());
            Assert.Equal("CEM-STD", root.GetProperty("itemCode").GetString());
            Assert.Equal(5.00m, root.GetProperty("unitPrice").GetDecimal());
            Assert.Equal("EUR", root.GetProperty("currency").GetString());
            Assert.Equal("kg", root.GetProperty("unit").GetString());
            Assert.Equal("compras@proveedor-test.local", root.GetProperty("contactEmail").GetString());
        }

        [Fact]
        public async Task GetPrice_WithDescription_FindsByFuzzySearch()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.GetPrice(itemDescription: "cemento premium obra", itemCode: null);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.True(root.GetProperty("found").GetBoolean());
            Assert.Equal("CEM-PREMIUM", root.GetProperty("itemCode").GetString());
        }

        [Fact]
        public async Task GetPrice_WithOnSaleProduct_ReturnsEffectiveAndOriginalPrice()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.GetPrice(itemDescription: "cemento oferta", itemCode: null);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.True(root.GetProperty("found").GetBoolean());
            Assert.True(root.GetProperty("isOnSale").GetBoolean());
            Assert.Equal(4.50m, root.GetProperty("unitPrice").GetDecimal());      // EffectivePrice
            Assert.Equal(6.00m, root.GetProperty("originalPrice").GetDecimal());  // UnitPrice original
        }

        [Fact]
        public async Task GetPrice_WhenNotFound_ReturnsFoundFalse()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.GetPrice(itemDescription: "material inexistente xyz", itemCode: null);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.False(root.GetProperty("found").GetBoolean());
        }

        private PricingTools CreateTools()
        {
            var repository = new EfCatalogRepository(_db);
            var service = new CatalogSearchService(repository, new TestSupplierConfig());
            return new PricingTools(service);
        }

        private void SeedCatalog()
        {
            var now = DateTime.UtcNow;

            _db.Products.AddRange(
                new CatalogProduct
                {
                    ItemCode = "CEM-PREMIUM",
                    Description = "Cemento premium estructural 42.5R",
                    Category = "cementos",
                    Keywords = "cemento,premium,estructural,obra",
                    Unit = "kg",
                    UnitPrice = 8.00m,
                    Currency = "EUR",
                    QualityRating = 5,
                    IsOnSale = false,
                    PackSize = 25,
                    PackPrice = 170,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "CEM-STD",
                    Description = "Cemento estándar para albañilería",
                    Category = "cementos",
                    Keywords = "cemento,estandar,albañileria",
                    Unit = "kg",
                    UnitPrice = 5.00m,
                    Currency = "EUR",
                    QualityRating = 3,
                    IsOnSale = false,
                    PackSize = 20,
                    PackPrice = 92,
                    UpdatedAt = now,
                    IsActive = true
                },
                new CatalogProduct
                {
                    ItemCode = "CEM-OFFER",
                    Description = "Cemento oferta especial de temporada",
                    Category = "cementos",
                    Keywords = "cemento,oferta,promo",
                    Unit = "kg",
                    UnitPrice = 6.00m,
                    SalePrice = 4.50m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = true,
                    PackSize = 30,
                    PackPrice = 125,
                    UpdatedAt = now,
                    IsActive = true
                });

            _db.SaveChanges();
        }
    }
}
