using IgnakeeAI.McpServer.Supplier.Application.Services;
using IgnakeeAI.McpServer.Supplier.Application.Interfaces;
using IgnakeeAI.McpServer.Supplier.Domain.Entities;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence.Repositories;
using IgnakeeAI.McpServer.Supplier.McpTools;
using IgnakeeAI.McpServer.Supplier.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace IgnakeeAI.McpServer.Supplier.Tests;

public sealed class AvailabilityToolsTests : IDisposable
{
    private readonly SupplierCatalogDbContext _db;

    public AvailabilityToolsTests()
    {
        var options = new DbContextOptionsBuilder<SupplierCatalogDbContext>()
            .UseInMemoryDatabase($"AvailabilityTools_{Guid.NewGuid()}")
            .Options;
        _db = new SupplierCatalogDbContext(options);
        _db.Products.Add(new CatalogProduct
        {
            ItemCode = "CEM-001", Description = "Cemento 25kg", Category = "cementos",
            Unit = "saco", UnitPrice = 5.90m, Currency = "EUR", AvailableStock = 120,
            LeadTimeDays = 1, IsActive = true
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task CheckAvailability_ExistingProduct_ReturnsStockAndLeadTime()
    {
        var result = await CreateTools().CheckAvailability("CEM-001");
        using var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        Assert.True(root.GetProperty("found").GetBoolean());
        Assert.Equal("CEM-001", root.GetProperty("itemCode").GetString());
        Assert.Equal(120, root.GetProperty("availableStock").GetDecimal());
        Assert.Equal(1, root.GetProperty("leadTimeDays").GetInt32());
        Assert.Equal("Disponible", root.GetProperty("message").GetString());
        Assert.DoesNotContain("AvailableStock", result);
    }

    [Fact]
    public async Task CheckAvailability_UnknownProduct_ReturnsFoundFalse()
    {
        var result = await CreateTools().CheckAvailability("UNKNOWN");
        using var json = JsonDocument.Parse(result);
        Assert.False(json.RootElement.GetProperty("found").GetBoolean());
    }

    [Fact]
    public async Task CheckAvailability_WithEnabledEcommerceInventory_UsesLiveAvailability()
    {
        var result = await CreateTools(new FakeEcommerceInventoryService(
            enabled: true,
            product: new CatalogProduct
            {
                ItemCode = "CEM-001",
                Description = "Cemento 25kg",
                Category = "cementos",
                Unit = "saco",
                UnitPrice = 5.90m,
                Currency = "EUR",
                AvailableStock = 3,
                LeadTimeDays = 4,
                IsActive = true
            })).CheckAvailability("CEM-001");

        using var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        Assert.True(root.GetProperty("found").GetBoolean());
        Assert.Equal(3, root.GetProperty("availableStock").GetDecimal());
        Assert.Equal(4, root.GetProperty("leadTimeDays").GetInt32());
    }

    [Fact]
    public async Task CheckAvailability_WhenEcommerceInventoryFails_FallsBackToLocalCatalog()
    {
        var result = await CreateTools(new FakeEcommerceInventoryService(
            enabled: true,
            exception: new Application.Contracts.EcommerceInventoryException(
                Application.Contracts.EcommerceInventoryFailureKind.Technical,
                "upstream failed"))).CheckAvailability("CEM-001");

        using var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        Assert.True(root.GetProperty("found").GetBoolean());
        Assert.Equal(120, root.GetProperty("availableStock").GetDecimal());
        Assert.Equal(1, root.GetProperty("leadTimeDays").GetInt32());
    }

    [Fact]
    public async Task CheckAvailability_EmptyCode_ThrowsValidationError() =>
        await Assert.ThrowsAsync<ArgumentException>(() => CreateTools().CheckAvailability(" "));

    public void Dispose() => _db.Dispose();

    private AvailabilityTools CreateTools(IEcommerceInventoryService? ecommerceInventory = null) => new(
        new CatalogSearchService(new EfCatalogRepository(_db), new TestSupplierConfig()),
        new TestSupplierConfig(),
        ecommerceInventory);

    private sealed class FakeEcommerceInventoryService : IEcommerceInventoryService
    {
        private readonly CatalogProduct? _product;
        private readonly Exception? _exception;

        public FakeEcommerceInventoryService(bool enabled, CatalogProduct? product = null, Exception? exception = null)
        {
            IsEnabled = enabled;
            _product = product;
            _exception = exception;
        }

        public bool IsEnabled { get; }

        public Task<CatalogProduct?> FindByCodeAsync(string itemCode, CancellationToken ct = default)
        {
            if (_exception is not null)
                throw _exception;

            return Task.FromResult(_product);
        }

        public Task<Application.Contracts.EcommerceInventorySyncResult> SyncCatalogAsync(CancellationToken ct = default) =>
            throw new NotSupportedException();
    }
}
