using IgnakeeAI.McpServer.Supplier.Application.Services;
using IgnakeeAI.McpServer.Supplier.Domain.Entities;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence.Repositories;
using IgnakeeAI.McpServer.Supplier.McpTools;
using IgnakeeAI.McpServer.Supplier.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace IgnakeeAI.McpServer.Supplier.Tests;

public sealed class BusinessHoursToolsTests : IDisposable
{
    private readonly SupplierCatalogDbContext _db;

    public BusinessHoursToolsTests()
    {
        var options = new DbContextOptionsBuilder<SupplierCatalogDbContext>()
            .UseInMemoryDatabase($"BusinessHoursTools_{Guid.NewGuid()}")
            .Options;
        _db = new SupplierCatalogDbContext(options);
    }

    [Fact]
    public void GetBusinessHours_ReturnsCamelCasePublicContactContract()
    {
        var result = CreateTools().GetBusinessHours();
        using var json = JsonDocument.Parse(result);
        var root = json.RootElement;

        Assert.Equal("L-V 08:00-18:00", root.GetProperty("hours").GetString());
        Assert.Equal("Proveedor Test", root.GetProperty("vendorName").GetString());
        Assert.Equal("compras@proveedor-test.local", root.GetProperty("contactEmail").GetString());
        Assert.DoesNotContain("ContactEmail", result);
    }

    public void Dispose() => _db.Dispose();

    private AvailabilityTools CreateTools() => new(
        new CatalogSearchService(new EfCatalogRepository(_db), new TestSupplierConfig()),
        new TestSupplierConfig());
}
