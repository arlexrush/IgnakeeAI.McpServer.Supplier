using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Text.Json;
using Xunit;
using System.Threading.Tasks; // Asegura que ValueTask esté disponible

namespace IgnakeeAI.McpServer.Supplier.Tests.Integration
{
    /// <summary>
    /// Pruebas de integración para los endpoints de salud y metadatos del servidor.
    /// Verifica que el servidor arranque correctamente y exponga la información esperada.
    /// </summary>
    public class HealthAndMetadataIntegrationTests : IClassFixture<SupplierApiFactory>, IAsyncLifetime, IAsyncDisposable
    {
        private readonly SupplierApiFactory _factory;
        private readonly HttpClient _client;
        private IServiceScope? _scope;

        public HealthAndMetadataIntegrationTests(SupplierApiFactory factory)
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

        // ── GET /health ───────────────────────────────────────────────────────────

        [Fact]
        public async Task GET_Health_Returns200()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;

            // Act
            var response = await _client.GetAsync("/health", ct);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GET_Health_ReturnsHealthyStatus()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;

            // Act
            var response = await _client.GetAsync("/health", ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            // Assert
            Assert.Contains("Healthy", body);
        }

        // ── GET / ─────────────────────────────────────────────────────────────────

        [Fact]
        public async Task GET_Root_Returns200()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;

            // Act
            var response = await _client.GetAsync("/", ct);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task GET_Root_ReturnsServerMetadata()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;

            // Act            
            var response = await _client.GetAsync("/", ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            // Assert
            Assert.Equal("IgnakeeAI MCP Supplier Server", root.GetProperty("server").GetString());
            Assert.Equal("/mcp", root.GetProperty("mcp_endpoint").GetString());
        }

        [Fact]
        public async Task GET_Root_ReturnsAllFourTools()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;

            // Act
            var response = await _client.GetAsync("/", ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var tools = doc.RootElement
                .GetProperty("tools")
                .EnumerateArray()
                .Select(t => t.GetString())
                .ToList();

            // Assert
            Assert.Contains("getPrice", tools);
            Assert.Contains("searchAlternatives", tools);
            Assert.Contains("checkAvailability", tools);
            Assert.Contains("getBusinessHours", tools);
        }

        [Fact]
        public async Task GET_Root_ReturnsApplicationJsonContentType()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;

            // Act
            var response = await _client.GetAsync("/", ct);
            // Assert
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        }
    }
}
