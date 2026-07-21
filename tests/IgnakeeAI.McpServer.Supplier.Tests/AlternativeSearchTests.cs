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
    public class AlternativeSearchTests : IDisposable
    {
        private readonly SupplierCatalogDbContext _db;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public AlternativeSearchTests()
        {
            var options = new DbContextOptionsBuilder<SupplierCatalogDbContext>()
                .UseInMemoryDatabase(databaseName: $"AlternativeToolsTest_{Guid.NewGuid()}")
                .Options;

            _db = new SupplierCatalogDbContext(options);
            SeedCatalog();
        }

        [Fact]
        public async Task SearchAlternatives_WithBetterCriteria_ReturnsHighQualityProducts()
        {
            var json = await CreateTools().SearchAlternatives(
                "cemento", "cementos", "better", maxResults: 5);
            using var document = JsonDocument.Parse(json);

            Assert.True(document.RootElement.GetProperty("found").GetBoolean());
            Assert.All(document.RootElement.GetProperty("alternatives").EnumerateArray(), item =>
                Assert.True(item.GetProperty("qualityRating").GetDecimal() >= 4));
        }

        [Fact]
        public async Task SearchAlternatives_WithAnyCriteria_ReturnsAlternatives()
        {
            var json = await CreateTools().SearchAlternatives(
                "cemento premium", "cementos", "any", maxResults: 2);
            using var document = JsonDocument.Parse(json);

            Assert.True(document.RootElement.GetProperty("found").GetBoolean());
            Assert.True(document.RootElement.GetProperty("count").GetInt32() <= 2);
        }

        [Fact]
        public async Task SearchAlternatives_WithInvalidLimit_ThrowsValidationError() =>
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => CreateTools().SearchAlternatives(
                "cemento", "cementos", "any", maxResults: 0));

        public void Dispose()
        {
            _cts.Dispose();
            _db.Dispose();
            GC.SuppressFinalize(this);
        }

        [Fact]
        public async Task SearchAlternatives_WithCheaperCriteria_ReturnsCheaperProducts()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.SearchAlternatives(
                itemDescription: "cemento premium",
                category: "cementos",
                criteria: "cheaper",
                maxResults: 5);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.True(root.GetProperty("found").GetBoolean());
            Assert.True(root.GetProperty("count").GetInt32() >= 1);

            var alternatives = root.GetProperty("alternatives").EnumerateArray().ToList();
            Assert.All(alternatives, alt =>
            {
                var unitPrice = alt.GetProperty("unitPrice").GetDecimal();
                Assert.True(unitPrice < 8.00m);
                Assert.Contains("Más económico", alt.GetProperty("reason").GetString());
            });
        }

        [Fact]
        public async Task SearchAlternatives_WithOnSaleCriteria_ReturnsOnlyOnSaleProducts()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.SearchAlternatives(
                itemDescription: "cemento",
                category: "cementos",
                criteria: "onSale",
                maxResults: 5);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.True(root.GetProperty("found").GetBoolean());

            var alternatives = root.GetProperty("alternatives").EnumerateArray().ToList();
            Assert.NotEmpty(alternatives);

            Assert.All(alternatives, alt =>
            {
                Assert.True(alt.GetProperty("isOnSale").GetBoolean());
                Assert.Contains("En oferta", alt.GetProperty("reason").GetString());
            });
        }

        [Fact]
        public async Task SearchAlternatives_WithOptimalPack_ReturnsPackOptimizationReason()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.SearchAlternatives(
                itemDescription: "cemento",
                category: "cementos",
                criteria: "optimalPack",
                requiredQuantity: 26,
                maxResults: 3);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.True(root.GetProperty("found").GetBoolean());

            var alternatives = root.GetProperty("alternatives").EnumerateArray().ToList();
            Assert.NotEmpty(alternatives);
            Assert.Contains("Presentación óptima", alternatives[0].GetProperty("reason").GetString());
        }

        [Fact]
        public async Task SearchAlternatives_WithInvalidCriteria_FallsBackToAny()
        {
            // Arrange
            var tools = CreateTools();

            // Act / Assert: los criterios desconocidos son un error de contrato.
            await Assert.ThrowsAsync<ArgumentException>(() => tools.SearchAlternatives(
                itemDescription: "cemento premium",
                category: "cementos",
                criteria: "invalid-criteria",
                requiredQuantity: 26,
                maxResults: 5));
        }

        [Fact]
        public async Task SearchAlternatives_WithoutCategory_InfersCategoryFromDescription()
        {
            // Arrange
            var tools = CreateTools();

            // Act
            var json = await tools.SearchAlternatives(
                itemDescription: "cemento oferta",
                category: null,
                criteria: "onSale",
                maxResults: 5);

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            // Assert
            Assert.True(root.GetProperty("found").GetBoolean());
            var first = root.GetProperty("alternatives").EnumerateArray().First();
            Assert.Equal("CEM-OFFER", first.GetProperty("itemCode").GetString());
        }

        private AlternativeSearchTools CreateTools()
        {
            var repository = new EfCatalogRepository(_db);
            var service = new CatalogSearchService(repository, new TestSupplierConfig());
            return new AlternativeSearchTools(service);
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
                },
                new CatalogProduct
                {
                    ItemCode = "STEEL-001",
                    Description = "Acero corrugado B500S",
                    Category = "aceros",
                    Keywords = "acero,corrugado,b500s",
                    Unit = "kg",
                    UnitPrice = 1.20m,
                    Currency = "EUR",
                    QualityRating = 4,
                    IsOnSale = false,
                    PackSize = 1000,
                    PackPrice = 1150,
                    UpdatedAt = now,
                    IsActive = true
                });

            _db.SaveChanges();
        }
    }
}
