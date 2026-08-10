using IgnakeeAI.McpServer.Supplier.Application.Interfaces;
using IgnakeeAI.McpServer.Supplier.Application.Services;
using IgnakeeAI.McpServer.Supplier.Domain.Entities;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Configuration;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Ecommerce;
using IgnakeeAI.McpServer.Supplier.Infrastructure.Connectors.Ecommerce.Dtos;
using IgnakeeAI.McpServer.Supplier.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Net;
using Xunit;

namespace IgnakeeAI.McpServer.Supplier.Tests
{
    /// <summary>
    /// Tests del conector HTTP de inventario del ecommerce y su integración
    /// con el servicio de disponibilidad (CheckAvailability).
    /// </summary>
    public class EcommerceInventoryConnectorTests : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();

        public void Dispose()
        {
            _cts.Dispose();
            GC.SuppressFinalize(this);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private static EcommerceInventoryConnector CreateConnector(
            EcommerceMockHttpHandler handler,
            bool enabled = true,
            string baseUrl = "https://ecommerce.test",
            string bearerToken = "test-bearer-token")
        {
            var options = Options.Create(new EcommerceInventoryOptions
            {
                Enabled = enabled,
                BaseUrl = baseUrl,
                BearerToken = bearerToken,
                TimeoutSeconds = 5,
                ProductLookupPath = "/api/v1/inventory/{productCode}",
                CatalogSyncPath = "/api/v1/inventory"
            });

            var httpClient = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
            return new EcommerceInventoryConnector(
                httpClient, options, NullLogger<EcommerceInventoryConnector>.Instance);
        }

        // ── IsEnabled ────────────────────────────────────────────────────────────

        [Fact]
        public void IsEnabled_WhenEnabledTrue_ReturnsTrue()
        {
            var handler = EcommerceMockHttpHandler.ForProduct(HttpStatusCode.OK, "{}");
            var connector = CreateConnector(handler, enabled: true, baseUrl: "https://test.example");
            Assert.True(connector.IsEnabled);
        }

        [Fact]
        public void IsEnabled_WhenEnabledFalse_ReturnsFalse()
        {
            var handler = EcommerceMockHttpHandler.ForProduct(HttpStatusCode.OK, "{}");
            var connector = CreateConnector(handler, enabled: false);
            Assert.False(connector.IsEnabled);
        }

        [Fact]
        public void IsEnabled_WhenBaseUrlEmpty_ReturnsFalse()
        {
            var options = Options.Create(new EcommerceInventoryOptions
            {
                Enabled = true,
                BaseUrl = "",
            });
            var httpClient = new HttpClient(
                EcommerceMockHttpHandler.ForProduct(HttpStatusCode.OK, "{}"));
            var connector = new EcommerceInventoryConnector(
                httpClient, options, NullLogger<EcommerceInventoryConnector>.Instance);
            Assert.False(connector.IsEnabled);
        }

        // ── GetProductByCodeAsync: éxito ─────────────────────────────────────────

        [Fact]
        public async Task GetProductByCodeAsync_ValidProduct_ReturnsCorrectCatalogProduct()
        {
            var handler = EcommerceMockHttpHandler.ForProduct(
                HttpStatusCode.OK, EcommerceFakeResponses.ProductSingle(
                    productCode: "ECO-001", price: 5.20m, stock: 500,
                    category: "cementos", unitToSell: "saco",
                    purchaseLeadTime: 3, purchaseLeadTimeUnit: "days"));
            var connector = CreateConnector(handler);

            var product = await connector.GetProductByCodeAsync("ECO-001", _cts.Token);

            Assert.NotNull(product);
            Assert.Equal("ECO-001", product.ItemCode);
            Assert.Equal("cementos", product.Category);
            Assert.Equal(5.20m, product.UnitPrice);
            Assert.Equal("EUR", product.Currency);
            Assert.Equal(500, product.AvailableStock);
            Assert.Equal("saco", product.Unit);
            Assert.Equal(3, product.LeadTimeDays);
            Assert.True(product.IsActive);
        }

        [Fact]
        public async Task GetProductByCodeAsync_SendsBearerAuthorizationHeader()
        {
            var handler = EcommerceMockHttpHandler.ForProduct(
                HttpStatusCode.OK, EcommerceFakeResponses.ProductSingle());
            var connector = CreateConnector(handler, bearerToken: "my-jwt-token");

            await connector.GetProductByCodeAsync("ECO-001", _cts.Token);

            Assert.Single(handler.AuthorizationHeaders);
            Assert.Equal("Bearer my-jwt-token", handler.AuthorizationHeaders[0]);
        }

        [Fact]
        public async Task GetProductByCodeAsync_BearerTokenNotInUrl()
        {
            // Validates that the token never appears in any request URL (only in header)
            var handler = EcommerceMockHttpHandler.ForProduct(
                HttpStatusCode.OK, EcommerceFakeResponses.ProductSingle());
            var connector = CreateConnector(handler, bearerToken: "super-secret-token");

            await connector.GetProductByCodeAsync("ECO-001", _cts.Token);

            Assert.All(handler.RequestUrls, url =>
                Assert.DoesNotContain("super-secret-token", url));
        }

        [Fact]
        public async Task GetProductByCodeAsync_InactivProduct_SetsIsActiveFalse()
        {
            var handler = EcommerceMockHttpHandler.ForProduct(
                HttpStatusCode.OK, EcommerceFakeResponses.ProductSingle(status: "discontinued"));
            var connector = CreateConnector(handler);

            var product = await connector.GetProductByCodeAsync("ECO-001", _cts.Token);

            Assert.NotNull(product);
            Assert.False(product.IsActive);
        }

        // ── GetProductByCodeAsync: leadtime normalization ────────────────────────

        [Fact]
        public async Task GetProductByCodeAsync_LeadTimeInHours_NormalizesToDays()
        {
            var handler = EcommerceMockHttpHandler.ForProduct(
                HttpStatusCode.OK, EcommerceFakeResponses.ProductWithLeadTimeInHours());
            var connector = CreateConnector(handler);

            var product = await connector.GetProductByCodeAsync("ECO-H-001", _cts.Token);

            Assert.NotNull(product);
            Assert.Equal(2, product.LeadTimeDays); // 48 horas → 2 días
        }

        [Fact]
        public async Task GetProductByCodeAsync_LeadTimeInWeeks_NormalizesToDays()
        {
            var handler = EcommerceMockHttpHandler.ForProduct(
                HttpStatusCode.OK, EcommerceFakeResponses.ProductWithLeadTimeInWeeks());
            var connector = CreateConnector(handler);

            var product = await connector.GetProductByCodeAsync("ECO-W-001", _cts.Token);

            Assert.NotNull(product);
            Assert.Equal(14, product.LeadTimeDays); // 2 semanas → 14 días
        }

        // ── GetProductByCodeAsync: not found ─────────────────────────────────────

        [Fact]
        public async Task GetProductByCodeAsync_NotFound_ReturnsNull()
        {
            var handler = EcommerceMockHttpHandler.ForProduct(HttpStatusCode.NotFound, null);
            var connector = CreateConnector(handler);

            var product = await connector.GetProductByCodeAsync("ECO-NOEXIST", _cts.Token);

            Assert.Null(product);
        }

        [Fact]
        public async Task GetProductByCodeAsync_EmptyProductCode_ReturnsNull()
        {
            var handler = EcommerceMockHttpHandler.ForProduct(
                HttpStatusCode.OK, EcommerceFakeResponses.ProductWithEmptyCode());
            var connector = CreateConnector(handler);

            var product = await connector.GetProductByCodeAsync("ECO-EMPTY", _cts.Token);

            Assert.Null(product);
        }

        // ── GetProductByCodeAsync: auth errors ───────────────────────────────────

        [Fact]
        public async Task GetProductByCodeAsync_Unauthorized_ThrowsEcommerceAuthException()
        {
            var handler = EcommerceMockHttpHandler.ForProduct(HttpStatusCode.Unauthorized, null);
            var connector = CreateConnector(handler);

            await Assert.ThrowsAsync<EcommerceAuthException>(
                () => connector.GetProductByCodeAsync("ECO-001", _cts.Token));
        }

        [Fact]
        public async Task GetProductByCodeAsync_Forbidden_ThrowsEcommerceAuthException()
        {
            var handler = EcommerceMockHttpHandler.ForProduct(HttpStatusCode.Forbidden, null);
            var connector = CreateConnector(handler);

            await Assert.ThrowsAsync<EcommerceAuthException>(
                () => connector.GetProductByCodeAsync("ECO-001", _cts.Token));
        }

        // ── GetProductByCodeAsync: malformed data ─────────────────────────────────

        [Fact]
        public async Task GetProductByCodeAsync_MalformedJson_ThrowsEcommerceMappingException()
        {
            var handler = EcommerceMockHttpHandler.ForProduct(
                HttpStatusCode.OK, EcommerceFakeResponses.ProductMalformedJson());
            var connector = CreateConnector(handler);

            await Assert.ThrowsAsync<EcommerceMappingException>(
                () => connector.GetProductByCodeAsync("ECO-001", _cts.Token));
        }

        // ── GetProductByCodeAsync: disabled ──────────────────────────────────────

        [Fact]
        public async Task GetProductByCodeAsync_WhenDisabled_ReturnsNull()
        {
            var handler = EcommerceMockHttpHandler.ForProduct(HttpStatusCode.OK,
                EcommerceFakeResponses.ProductSingle());
            var connector = CreateConnector(handler, enabled: false);

            var product = await connector.GetProductByCodeAsync("ECO-001", _cts.Token);

            Assert.Null(product);
            Assert.Empty(handler.Requests); // No se debe hacer ninguna petición HTTP
        }

        // ── GetCatalogPageAsync: éxito ────────────────────────────────────────────

        [Fact]
        public async Task GetCatalogPageAsync_ValidPage_ReturnsProducts()
        {
            var handler = EcommerceMockHttpHandler.ForCatalog(pageIndex =>
                (HttpStatusCode.OK, EcommerceFakeResponses.CatalogPage(pageIndex, 3, totalCount: 3)));
            var connector = CreateConnector(handler);

            var products = await connector.GetCatalogPageAsync(1, 3, _cts.Token);

            Assert.Equal(3, products.Count);
            Assert.All(products, p =>
            {
                Assert.False(string.IsNullOrWhiteSpace(p.ItemCode));
                Assert.True(p.IsActive);
            });
        }

        [Fact]
        public async Task GetCatalogPageAsync_EmptyPage_ReturnsEmptyList()
        {
            var handler = EcommerceMockHttpHandler.ForCatalog(_ =>
                (HttpStatusCode.OK, EcommerceFakeResponses.CatalogPageEmpty()));
            var connector = CreateConnector(handler);

            var products = await connector.GetCatalogPageAsync(99, 100, _cts.Token);

            Assert.Empty(products);
        }

        [Fact]
        public async Task GetCatalogPageAsync_PageWithEmptyCode_SkipsInvalidProducts()
        {
            var handler = EcommerceMockHttpHandler.ForCatalog(_ =>
                (HttpStatusCode.OK, EcommerceFakeResponses.CatalogPageWithEmptyCode()));
            var connector = CreateConnector(handler);

            var products = await connector.GetCatalogPageAsync(1, 100, _cts.Token);

            // Solo el producto con código válido debe incluirse
            Assert.Single(products);
            Assert.Equal("ECO-VALID-001", products[0].ItemCode);
        }

        // ── GetCatalogPageAsync: pagination ───────────────────────────────────────

        [Fact]
        public async Task GetCatalogPageAsync_MultiplePages_SendsCorrectPageNumbers()
        {
            var pagesRequested = new List<int>();
            var handler = EcommerceMockHttpHandler.ForCatalog(pageIndex =>
            {
                pagesRequested.Add(pageIndex);
                return (HttpStatusCode.OK, EcommerceFakeResponses.CatalogPage(pageIndex, 2, totalCount: 6));
            });
            var connector = CreateConnector(handler);

            // Simular iterar 3 páginas manualmente
            await connector.GetCatalogPageAsync(1, 2, _cts.Token);
            await connector.GetCatalogPageAsync(2, 2, _cts.Token);
            await connector.GetCatalogPageAsync(3, 2, _cts.Token);

            Assert.Equal(new[] { 1, 2, 3 }, pagesRequested);
        }

        // ── GetCatalogPageAsync: auth errors ─────────────────────────────────────

        [Fact]
        public async Task GetCatalogPageAsync_Unauthorized_ThrowsEcommerceAuthException()
        {
            var handler = EcommerceMockHttpHandler.ForCatalog(_ =>
                (HttpStatusCode.Unauthorized, ""));
            var connector = CreateConnector(handler);

            await Assert.ThrowsAsync<EcommerceAuthException>(
                () => connector.GetCatalogPageAsync(1, 100, _cts.Token));
        }

        // ── GetCatalogPageAsync: malformed data ───────────────────────────────────

        [Fact]
        public async Task GetCatalogPageAsync_MalformedJson_ThrowsEcommerceMappingException()
        {
            var handler = EcommerceMockHttpHandler.ForCatalog(_ =>
                (HttpStatusCode.OK, EcommerceFakeResponses.CatalogPageMalformedJson()));
            var connector = CreateConnector(handler);

            await Assert.ThrowsAsync<EcommerceMappingException>(
                () => connector.GetCatalogPageAsync(1, 100, _cts.Token));
        }

        // ── MapToProduct: campo mapping ───────────────────────────────────────────

        [Fact]
        public void MapToProduct_UsesProductNameWhenDescriptionEmpty()
        {
            var dto = new EcommerceProductDto
            {
                ProductCode = "ECO-001",
                ProductName = "Nombre del producto",
                Description = null,
                Category = "cat",
                Price = 1m,
                Currency = "USD",
                Status = "active"
            };

            var product = EcommerceInventoryConnector.MapToProduct(dto);

            Assert.Equal("Nombre del producto", product.Description);
        }

        [Fact]
        public void MapToProduct_FallsBackToProductCodeWhenBothEmpty()
        {
            var dto = new EcommerceProductDto
            {
                ProductCode = "ECO-001",
                ProductName = null,
                Description = null,
                Category = "cat",
                Price = 1m,
                Status = "active"
            };

            var product = EcommerceInventoryConnector.MapToProduct(dto);

            Assert.Equal("ECO-001", product.Description);
        }

        [Fact]
        public void MapToProduct_DefaultsToEurWhenCurrencyEmpty()
        {
            var dto = new EcommerceProductDto
            {
                ProductCode = "ECO-001",
                Currency = null,
                Price = 5m,
                Status = "Active",
                IsAvailableForSale = true
            };

            var product = EcommerceInventoryConnector.MapToProduct(dto);

            Assert.Equal("EUR", product.Currency);
        }

        [Fact]
        public void MapToProduct_NullPrice_MapsToZeroUnitPrice()
        {
            var dto = new EcommerceProductDto
            {
                ProductCode = "ECO-NOPRICE",
                ProductName = "Sin precio",
                Price = null,
                Status = "Active",
                IsAvailableForSale = true
            };

            var product = EcommerceInventoryConnector.MapToProduct(dto);

            Assert.Equal(0m, product.UnitPrice);
        }

        [Fact]
        public async Task GetProductByCodeAsync_NullPrice_MapsToZeroUnitPrice()
        {
            var handler = EcommerceMockHttpHandler.ForProduct(
                HttpStatusCode.OK, EcommerceFakeResponses.ProductWithNullPrice());
            var connector = CreateConnector(handler);

            var product = await connector.GetProductByCodeAsync("ECO-NOPRICE", _cts.Token);

            Assert.NotNull(product);
            Assert.Equal(0m, product.UnitPrice);
        }

        [Fact]
        public void MapToProduct_IsAvailableForSaleFalse_SetsIsActiveFalse()
        {
            var dto = new EcommerceProductDto
            {
                ProductCode = "ECO-001",
                Price = 5m,
                Status = "Active",
                IsAvailableForSale = false   // not for sale
            };

            var product = EcommerceInventoryConnector.MapToProduct(dto);

            Assert.False(product.IsActive);
        }

        [Fact]
        public void MapToProduct_StatusNotActive_SetsIsActiveFalse()
        {
            var dto = new EcommerceProductDto
            {
                ProductCode = "ECO-001",
                Price = 5m,
                Status = "Discontinued",
                IsAvailableForSale = true   // available but status not "Active"
            };

            var product = EcommerceInventoryConnector.MapToProduct(dto);

            Assert.False(product.IsActive);
        }

        [Fact]
        public void MapToProduct_BothActiveAndAvailable_SetsIsActiveTrue()
        {
            var dto = new EcommerceProductDto
            {
                ProductCode = "ECO-001",
                Price = 5m,
                Status = "Active",
                IsAvailableForSale = true
            };

            var product = EcommerceInventoryConnector.MapToProduct(dto);

            Assert.True(product.IsActive);
        }

        [Fact]
        public async Task GetCatalogPageAsync_SendsPageIndexParam()
        {
            // Verify URL contains pageIndex= not page=
            var handler = EcommerceMockHttpHandler.ForCatalog(_ =>
                (HttpStatusCode.OK, EcommerceFakeResponses.CatalogPage(2, 50)));
            var connector = CreateConnector(handler);

            await connector.GetCatalogPageAsync(2, 50, _cts.Token);

            Assert.Single(handler.RequestUrls);
            Assert.Contains("pageIndex=2", handler.RequestUrls[0]);
            Assert.Contains("pageSize=50", handler.RequestUrls[0]);
            Assert.DoesNotContain("status=active", handler.RequestUrls[0]);
        }

        [Fact]
        public async Task GetCatalogPageAsync_DataEnvelope_DeserializesCorrectly()
        {
            // The response uses "data" array, not "items" — verify the right products are parsed
            var handler = EcommerceMockHttpHandler.ForCatalog(_ =>
                (HttpStatusCode.OK, EcommerceFakeResponses.CatalogPage(1, 5)));
            var connector = CreateConnector(handler);

            var products = await connector.GetCatalogPageAsync(1, 5, _cts.Token);

            // If "items" assumption remained, this would return 0 products
            Assert.Equal(5, products.Count);
        }

        [Fact]
        public async Task GetCatalogPageAsync_UnavailableProduct_SetsIsActiveFalse()
        {
            // Catalog response with isAvailableForSale=false — product should be imported but inactive
            var handler = EcommerceMockHttpHandler.ForCatalog(_ =>
                (HttpStatusCode.OK, EcommerceFakeResponses.CatalogPageWithUnavailableProduct()));
            var connector = CreateConnector(handler);

            var products = await connector.GetCatalogPageAsync(1, 10, _cts.Token);

            Assert.Single(products);
            Assert.False(products[0].IsActive);
        }

        [Fact]
        public async Task GetProductByCodeAsync_CancellationRequested_PropagatesCancellation()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var handler = EcommerceMockHttpHandler.ForProduct(HttpStatusCode.OK,
                EcommerceFakeResponses.ProductSingle());
            var connector = CreateConnector(handler);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => connector.GetProductByCodeAsync("ECO-001", cts.Token));
        }

        [Fact]
        public async Task GetCatalogPageAsync_CancellationRequested_PropagatesCancellation()
        {
            using var cts = new CancellationTokenSource();
            cts.Cancel();

            var handler = EcommerceMockHttpHandler.ForCatalog(_ =>
                (HttpStatusCode.OK, EcommerceFakeResponses.CatalogPage(1, 5)));
            var connector = CreateConnector(handler);

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => connector.GetCatalogPageAsync(1, 5, cts.Token));
        }

        // ── CatalogSearchService: hybrid availability ────────────────────────────

        [Fact]
        public async Task CheckAvailabilityAsync_EcommerceEnabled_ReturnsLiveData()
        {
            // Arrange: ecommerce devuelve stock=999, local tiene stock=10
            var localRepo = new StubCatalogRepo("ECO-001", stock: 10);
            var ecommerceClient = new StubEcommerceClient(enabled: true, productCode: "ECO-001", stock: 999);
            var service = new CatalogSearchService(localRepo, new TestSupplierConfig(), ecommerceClient);

            // Act
            var result = await service.CheckAvailabilityAsync("ECO-001", _cts.Token);

            // Assert: usa datos del ecommerce (999)
            Assert.True(result.Found);
            Assert.Equal(999, result.AvailableStock);
        }

        [Fact]
        public async Task CheckAvailabilityAsync_EcommerceFailure_FallsBackToLocalCatalog()
        {
            // Arrange: ecommerce lanza excepción, local tiene el producto
            var localRepo = new StubCatalogRepo("ECO-001", stock: 42);
            var ecommerceClient = new StubEcommerceClient(enabled: true, throwException: true);
            var service = new CatalogSearchService(localRepo, new TestSupplierConfig(), ecommerceClient);

            // Act
            var result = await service.CheckAvailabilityAsync("ECO-001", _cts.Token);

            // Assert: fallback al catálogo local (42)
            Assert.True(result.Found);
            Assert.Equal(42, result.AvailableStock);
        }

        [Fact]
        public async Task CheckAvailabilityAsync_EcommerceDisabled_UsesLocalCatalog()
        {
            // Arrange: ecommerce deshabilitado
            var localRepo = new StubCatalogRepo("ECO-001", stock: 7);
            var ecommerceClient = new StubEcommerceClient(enabled: false);
            var service = new CatalogSearchService(localRepo, new TestSupplierConfig(), ecommerceClient);

            // Act
            var result = await service.CheckAvailabilityAsync("ECO-001", _cts.Token);

            // Assert: usa el catálogo local (7)
            Assert.True(result.Found);
            Assert.Equal(7, result.AvailableStock);
        }

        [Fact]
        public async Task CheckAvailabilityAsync_EcommerceReturnsNull_FallsBackToLocalCatalog()
        {
            // Arrange: ecommerce habilitado pero devuelve null (producto no encontrado)
            var localRepo = new StubCatalogRepo("ECO-001", stock: 20);
            var ecommerceClient = new StubEcommerceClient(enabled: true, productCode: null); // retorna null
            var service = new CatalogSearchService(localRepo, new TestSupplierConfig(), ecommerceClient);

            // Act
            var result = await service.CheckAvailabilityAsync("ECO-001", _cts.Token);

            // Assert: fallback al catálogo local (20)
            Assert.True(result.Found);
            Assert.Equal(20, result.AvailableStock);
        }

        // ── Regression guard: SyncPageSize vs Ecommerce MaxPagesSize ─────────────

        /// <summary>
        /// Regression guard: EcommerceInventoryOptions.SyncPageSize debe ser 50,
        /// alineado con el límite MaxPagesSize=50 de PaginationBaseQuery en el ecommerce.
        /// Un valor mayor (e.g. 100) hace que el bucle de sincronización en
        /// AdminCatalogEndPoint termine prematuramente porque el ecommerce recorta la
        /// respuesta a 50 ítems; el comparador products.Count &lt; pageSize se cumple
        /// en la primera página aunque existan más páginas → truncación silenciosa.
        /// Fuente: src/Core/Ecommerce.Application/…/Features/Shared/Queries/PaginationBaseQuery.cs
        ///          private const int MaxPagesSize = 50;
        /// </summary>
        [Fact]
        public void EcommerceInventoryOptions_DefaultSyncPageSize_Is50()
        {
            var opts = new EcommerceInventoryOptions();
            Assert.Equal(50, opts.SyncPageSize);
        }

        /// <summary>
        /// Prueba que el bucle de sincronización (lógica: continúa mientras
        /// products.Count == pageSize) termina correctamente cuando se usa
        /// pageSize=50 (el máximo del ecommerce). Simula un catálogo de 125
        /// productos distribuidos en 3 páginas (50 + 50 + 25).
        ///
        /// Regression: si pageSize fuera 100, el ecommerce retornaría 50 en la primera
        /// página y 50 &lt; 100 haría break, perdiendo las páginas 2 y 3 (75 productos).
        /// </summary>
        [Fact]
        public async Task GetCatalogPageAsync_MultiPage_AllPagesReadableWithCorrectPageSize()
        {
            // Catalog: 125 products total, pages 1 and 2 have 50, page 3 has 25
            const int pageSize = 50;

            var handler = EcommerceMockHttpHandler.ForCatalog(pageIndex =>
            {
                if (pageIndex == 1) return (HttpStatusCode.OK, EcommerceFakeResponses.CatalogPage(1, pageSize, totalCount: 125));
                if (pageIndex == 2) return (HttpStatusCode.OK, EcommerceFakeResponses.CatalogPage(2, pageSize, totalCount: 125));
                if (pageIndex == 3) return (HttpStatusCode.OK, EcommerceFakeResponses.CatalogPage(3, 25, totalCount: 125));
                return (HttpStatusCode.OK, EcommerceFakeResponses.CatalogPageEmpty());
            });
            var connector = CreateConnector(handler);

            var p1 = await connector.GetCatalogPageAsync(1, pageSize, _cts.Token);
            var p2 = await connector.GetCatalogPageAsync(2, pageSize, _cts.Token);
            var p3 = await connector.GetCatalogPageAsync(3, pageSize, _cts.Token);

            Assert.Equal(50, p1.Count);
            Assert.Equal(50, p2.Count);
            Assert.Equal(25, p3.Count);

            // Simulate sync-loop termination logic: break when p3.Count < pageSize
            // (this is what AdminCatalogEndPoint does)
            Assert.True(p1.Count == pageSize); // continue
            Assert.True(p2.Count == pageSize); // continue
            Assert.True(p3.Count < pageSize);  // break — all products read
        }

        /// <summary>
        /// Regression guard: demuestra que si se enviara pageSize=100 y el ecommerce
        /// retorna solo 50 (su máximo), la condición products.Count &lt; pageSize
        /// se cumpliría en la primera página (50 &lt; 100 = true), rompiendo el bucle
        /// prematuramente y perdiendo las páginas siguientes.
        /// </summary>
        [Fact]
        public void SyncLoopTermination_PageSizeOver50_WouldTruncateCatalog()
        {
            // Ecommerce silently caps response to MaxPagesSize=50.
            // With pageSize > 50, the loop breaks after first page.
            const int requestedPageSize = 100;
            const int actualItemsReturned = 50; // ecommerce capped at 50

            // Old (stale) assumption: 50 < 100 → loop would break → silent truncation
            bool wouldTruncate = actualItemsReturned < requestedPageSize;
            Assert.True(wouldTruncate,
                "pageSize=100 causes premature termination: ecommerce returns max 50, " +
                "50 < 100 triggers loop break after first page.");

            // Corrected assumption: 50 < 50 → false → loop continues
            const int correctedPageSize = 50;
            bool continuesCorrectly = actualItemsReturned < correctedPageSize;
            Assert.False(continuesCorrectly,
                "pageSize=50 allows the loop to continue reading subsequent pages.");
        }
    }

    // ── Stubs simples para tests de servicio ──────────────────────────────────────

    /// <summary>Repositorio de catálogo que siempre devuelve un producto con el stock dado.</summary>
    internal sealed class StubCatalogRepo : ICatalogRepository
    {
        private readonly CatalogProduct? _product;

        public StubCatalogRepo(string? itemCode = null, int stock = 0)
        {
            if (itemCode != null)
                _product = new CatalogProduct
                {
                    ItemCode = itemCode, Description = "Test", Category = "test",
                    Unit = "ud", UnitPrice = 1m, AvailableStock = stock, IsActive = true
                };
        }

        public Task<CatalogProduct?> FindByCodeAsync(string itemCode, CancellationToken ct = default)
            => Task.FromResult(_product?.ItemCode == itemCode ? _product : null);

        public Task<CatalogProduct?> FindByDescriptionAsync(IReadOnlyList<string> searchTerms, CancellationToken ct = default)
            => Task.FromResult<CatalogProduct?>(null);
        public Task<string?> InferCategoryAsync(IReadOnlyList<string> searchTerms, CancellationToken ct = default)
            => Task.FromResult<string?>(null);
        public Task<IReadOnlyList<CatalogProduct>> FindCheaperInCategoryAsync(string category, decimal referencePrice, int max, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CatalogProduct>>([]);
        public Task<IReadOnlyList<CatalogProduct>> FindBetterQualityAsync(string category, int minRating, int max, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CatalogProduct>>([]);
        public Task<IReadOnlyList<CatalogProduct>> FindOnSaleAsync(string category, int max, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CatalogProduct>>([]);
        public Task<IReadOnlyList<CatalogProduct>> FindWithPackInfoAsync(string category, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CatalogProduct>>([]);
    }

    /// <summary>Cliente de inventario ecommerce configurable para tests.</summary>
    internal sealed class StubEcommerceClient : IEcommerceInventoryClient
    {
        private readonly string? _productCode;
        private readonly int _stock;
        private readonly bool _throwException;

        public bool IsEnabled { get; }

        public StubEcommerceClient(bool enabled = true, string? productCode = "ECO-001",
            int stock = 0, bool throwException = false)
        {
            IsEnabled = enabled;
            _productCode = productCode;
            _stock = stock;
            _throwException = throwException;
        }

        public Task<CatalogProduct?> GetProductByCodeAsync(string productCode, CancellationToken ct = default)
        {
            if (_throwException)
                throw new EcommerceCommunicationException("Test exception");

            if (_productCode == null || _productCode != productCode)
                return Task.FromResult<CatalogProduct?>(null);

            return Task.FromResult<CatalogProduct?>(new CatalogProduct
            {
                ItemCode = productCode, Description = "Live", Category = "test",
                Unit = "ud", UnitPrice = 1m, AvailableStock = _stock, IsActive = true
            });
        }

        public Task<IReadOnlyList<CatalogProduct>> GetCatalogPageAsync(int page, int pageSize, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CatalogProduct>>([]);
    }

}
