using IgnakeeAI.McpServer.Supplier.Application.Contracts;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Persistence;
using IgnakeeAI.McpServer.Supplier.Tests.Fakes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using Xunit;

namespace IgnakeeAI.McpServer.Supplier.Tests;

public sealed class EcommerceInventoryConnectorTests : IDisposable
{
    private readonly SupplierCatalogDbContext _db;

    public EcommerceInventoryConnectorTests()
    {
        var options = new DbContextOptionsBuilder<SupplierCatalogDbContext>()
            .UseInMemoryDatabase($"EcommerceInventory_{Guid.NewGuid()}")
            .Options;
        _db = new SupplierCatalogDbContext(options);
    }

    [Fact]
    public async Task FindByCodeAsync_WithValidProduct_MapsFieldsAndSendsAuthHeader()
    {
        var handler = new EcommerceInventoryMockHttpHandler((request, _) =>
            Task.FromResult(EcommerceInventoryMockHttpHandler.Json(
                HttpStatusCode.OK,
                """
                {
                  "productCode":"SKU-001",
                  "productId":"42",
                  "productName":"Cemento premium",
                  "description":"Cemento premium 25kg",
                  "category":"Cementos",
                  "price":9.5,
                  "currency":"eur",
                  "stock":12,
                  "unitToSell":"saco",
                  "purchaseLeadTime":48,
                  "purchaseLeadTimeUnit":"hours",
                  "status":"active"
                }
                """)));

        var connector = CreateConnector(handler);

        var product = await connector.FindByCodeAsync("SKU-001");

        Assert.NotNull(product);
        Assert.Equal("SKU-001", product!.ItemCode);
        Assert.Equal("Cemento premium 25kg", product.Description);
        Assert.Equal("cementos", product.Category);
        Assert.Equal(9.5m, product.UnitPrice);
        Assert.Equal("EUR", product.Currency);
        Assert.Equal(12, product.AvailableStock);
        Assert.Equal(2, product.LeadTimeDays);
        Assert.Equal("saco", product.Unit);
        Assert.Equal("secret-test-key", handler.Requests.Single().Headers["X-Api-Key"]);
        Assert.Contains("/api/inventory/v1/products/SKU-001", handler.Requests.Single().Url);
    }

    [Fact]
    public async Task FindByCodeAsync_WithNotFound_ReturnsNull()
    {
        var handler = new EcommerceInventoryMockHttpHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound)));

        var connector = CreateConnector(handler);

        var product = await connector.FindByCodeAsync("MISSING");

        Assert.Null(product);
    }

    [Fact]
    public async Task SyncCatalogAsync_WithPaginatedCatalog_UpsertsProducts()
    {
        _db.Products.Add(new Domain.Entities.CatalogProduct
        {
            ItemCode = "SKU-001",
            Description = "Anterior",
            Category = "cementos",
            Keywords = "old",
            Unit = "ud",
            UnitPrice = 1m,
            Currency = "EUR",
            IsActive = true
        });
        await _db.SaveChangesAsync();

        var handler = new EcommerceInventoryMockHttpHandler((request, _) =>
        {
            if (request.RequestUri!.Query.Contains("page=1", StringComparison.Ordinal))
            {
                return Task.FromResult(EcommerceInventoryMockHttpHandler.Json(
                    HttpStatusCode.OK,
                    """
                    {
                      "items": [
                        {
                          "productCode":"SKU-001",
                          "productId":"1",
                          "productName":"Cemento A",
                          "description":"Cemento actualizado",
                          "category":"Cementos",
                          "price":10.0,
                          "currency":"EUR",
                          "stock":20,
                          "unitToSell":"saco",
                          "purchaseLeadTime":2,
                          "purchaseLeadTimeUnit":"days",
                          "status":"active"
                        },
                        {
                          "productCode":"SKU-002",
                          "productId":"2",
                          "productName":"Cemento B",
                          "description":"Nuevo producto",
                          "category":"Cementos",
                          "price":11.5,
                          "currency":"EUR",
                          "stock":8,
                          "unitToSell":"saco",
                          "purchaseLeadTime":1,
                          "purchaseLeadTimeUnit":"days",
                          "status":"active"
                        }
                      ],
                      "pagination": {
                        "hasMore": true
                      }
                    }
                    """));
            }

            return Task.FromResult(EcommerceInventoryMockHttpHandler.Json(
                HttpStatusCode.OK,
                """
                {
                  "items": [
                    {
                      "productCode":"SKU-003",
                      "productId":"3",
                      "productName":"Pintura C",
                      "description":"Pintura exterior",
                      "category":"Pinturas",
                      "price":15.0,
                      "currency":"EUR",
                      "stock":5,
                      "unitToSell":"lata",
                      "purchaseLeadTime":1,
                      "purchaseLeadTimeUnit":"weeks",
                      "status":"active"
                    }
                  ],
                  "pagination": {
                    "hasMore": false
                  }
                }
                """));
        });

        var connector = CreateConnector(handler, pageSize: 2);

        var result = await connector.SyncCatalogAsync();

        Assert.Equal(3, result.ProductsRead);
        Assert.Equal(2, result.ProductsCreated);
        Assert.Equal(1, result.ProductsUpdated);
        Assert.Equal(0, result.ProductsRejected);
        Assert.Equal(3, _db.Products.Count());
        Assert.Equal(10.0m, _db.Products.Single(product => product.ItemCode == "SKU-001").UnitPrice);
        Assert.Equal(7, _db.Products.Single(product => product.ItemCode == "SKU-003").LeadTimeDays);
        Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task SyncCatalogAsync_WithMalformedProduct_RejectsInvalidEntryAndContinues()
    {
        var handler = new EcommerceInventoryMockHttpHandler((_, _) =>
            Task.FromResult(EcommerceInventoryMockHttpHandler.Json(
                HttpStatusCode.OK,
                """
                {
                  "items": [
                    {
                      "productCode":"SKU-001",
                      "productId":"1",
                      "productName":"Producto válido",
                      "description":"Producto válido",
                      "category":"General",
                      "price":4.2,
                      "currency":"EUR",
                      "stock":3,
                      "unitToSell":"ud",
                      "purchaseLeadTime":1,
                      "purchaseLeadTimeUnit":"days",
                      "status":"active"
                    },
                    {
                      "productCode":"SKU-INVALID",
                      "productId":"2",
                      "productName":"Producto inválido",
                      "description":"Producto inválido",
                      "category":"General",
                      "price":3.0,
                      "currency":"EUR",
                      "stock":-1,
                      "unitToSell":"ud",
                      "purchaseLeadTime":1,
                      "purchaseLeadTimeUnit":"days",
                      "status":"active"
                    }
                  ],
                  "pagination": {
                    "hasMore": false
                  }
                }
                """)));

        var connector = CreateConnector(handler);

        var result = await connector.SyncCatalogAsync();

        Assert.Equal(2, result.ProductsRead);
        Assert.Equal(1, result.ProductsCreated);
        Assert.Equal(0, result.ProductsUpdated);
        Assert.Equal(1, result.ProductsRejected);
        Assert.Single(_db.Products);
    }

    [Fact]
    public async Task FindByCodeAsync_WithUnauthorizedResponse_ThrowsAuthenticationException()
    {
        var handler = new EcommerceInventoryMockHttpHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)));

        var connector = CreateConnector(handler);

        var exception = await Assert.ThrowsAsync<EcommerceInventoryException>(() => connector.FindByCodeAsync("SKU-001"));

        Assert.Equal(EcommerceInventoryFailureKind.Authentication, exception.Kind);
    }

    [Fact]
    public async Task FindByCodeAsync_WithTimeout_ThrowsTimeoutException()
    {
        var handler = new EcommerceInventoryMockHttpHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            return EcommerceInventoryMockHttpHandler.Json(HttpStatusCode.OK, "{}");
        });

        var connector = CreateConnector(handler, requestTimeoutSeconds: 1);

        var exception = await Assert.ThrowsAsync<EcommerceInventoryException>(() => connector.FindByCodeAsync("SKU-001"));

        Assert.Equal(EcommerceInventoryFailureKind.Timeout, exception.Kind);
    }

    [Fact]
    public async Task FindByCodeAsync_WithServerError_ThrowsTechnicalException()
    {
        var handler = new EcommerceInventoryMockHttpHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadGateway)));

        var connector = CreateConnector(handler);

        var exception = await Assert.ThrowsAsync<EcommerceInventoryException>(() => connector.FindByCodeAsync("SKU-001"));

        Assert.Equal(EcommerceInventoryFailureKind.Technical, exception.Kind);
    }

    public void Dispose() => _db.Dispose();

    private EcommerceInventoryConnector CreateConnector(
        HttpMessageHandler handler,
        int requestTimeoutSeconds = 15,
        int pageSize = 100)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://inventory.example.com")
        };

        return new EcommerceInventoryConnector(
            httpClient,
            _db,
            Options.Create(new EcommerceInventoryOptions
            {
                Enabled = true,
                BaseUrl = "https://inventory.example.com",
                AuthenticationHeaderName = "X-Api-Key",
                AuthenticationHeaderValue = "secret-test-key",
                RequestTimeoutSeconds = requestTimeoutSeconds,
                ProductLookupPathTemplate = "/api/inventory/v1/products/{productCode}",
                CatalogSyncPathTemplate = "/api/inventory/v1/products/active?page={page}&pageSize={pageSize}",
                CatalogSyncPageSize = pageSize
            }),
            NullLogger<EcommerceInventoryConnector>.Instance);
    }
}
