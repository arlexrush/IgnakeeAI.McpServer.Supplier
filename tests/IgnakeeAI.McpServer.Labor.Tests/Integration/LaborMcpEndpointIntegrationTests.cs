using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Xunit;

namespace IgnakeeAI.McpServer.Labor.Tests.Integration
{
    /// <summary>
    /// Pruebas de integración para el endpoint MCP principal del Labor Server: POST /mcp.
    /// Verifica el flujo completo: recepción JSON-RPC → tool dispatch → WorkerSearchService → BD.
    /// </summary>
    public class LaborMcpEndpointIntegrationTests : IClassFixture<LaborApiFactory>, IAsyncLifetime
    {
        private static readonly Dictionary<string, string> ToolNameMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["getWorkerRate"] = "get_worker_rate",
            ["searchWorkers"] = "search_workers",
            ["getWorkerProfile"] = "get_worker_profile",
            ["checkWorkerAvailability"] = "check_worker_availability",
            ["getContactInfo"] = "get_contact_info"
        };

        private readonly ConcurrentDictionary<string, string> _resolvedToolNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly LaborApiFactory _factory;
        private readonly HttpClient _client;
        private IServiceScope? _scope;

        public LaborMcpEndpointIntegrationTests(LaborApiFactory factory)
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

        // ── POST /mcp — initialize ────────────────────────────────────────────────

        [Fact]
        public async Task POST_Mcp_Initialize_Returns200()
        {
            var ct = TestContext.Current.CancellationToken;
            var payload = new
            {
                jsonrpc = "2.0",
                id = 1,
                method = "initialize",
                @params = new
                {
                    protocolVersion = "2024-11-05",
                    capabilities = new { },
                    clientInfo = new { name = "TestClient", version = "1.0" }
                }
            };

            var response = await PostMcpAsync(payload, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // ── POST /mcp — tools/list ────────────────────────────────────────────────

        [Fact]
        public async Task POST_Mcp_ToolsList_ReturnsAllFiveTools()
        {
            var ct = TestContext.Current.CancellationToken;
            var payload = new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/list",
                @params = new { }
            };

            var response = await PostMcpAsync(payload, ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadAsStringAsync(ct);
            // Response may be SSE or JSON; check tool names appear in body
            Assert.Contains("get_worker_rate", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("search_workers", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("get_worker_profile", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("check_worker_availability", body, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("get_contact_info", body, StringComparison.OrdinalIgnoreCase);
        }

        // ── POST /mcp — tools/call: getWorkerRate ─────────────────────────────────

        [Fact]
        public async Task POST_Mcp_GetWorkerRate_ByWorkerId_ReturnsFoundTrue()
        {
            var ct = TestContext.Current.CancellationToken;
            var toolName = await ResolveToolNameAsync("getWorkerRate", ct);

            var payload = new
            {
                jsonrpc = "2.0",
                id = 3,
                method = "tools/call",
                @params = new
                {
                    name = toolName,
                    arguments = new
                    {
                        specialtyDescription = "",
                        workerId = "TRB-001"
                    }
                }
            };

            var result = await ExtractToolResultAsync(await PostMcpAsync(payload, ct));
            Assert.True(result.GetProperty("found").GetBoolean());
            Assert.Equal("TRB-001", result.GetProperty("workerId").GetString());
        }

        // ── POST /mcp — tools/call: checkWorkerAvailability ──────────────────────

        [Fact]
        public async Task POST_Mcp_CheckWorkerAvailability_ExistingWorker_ReturnsFoundTrue()
        {
            var ct = TestContext.Current.CancellationToken;
            var toolName = await ResolveToolNameAsync("checkWorkerAvailability", ct);

            var payload = new
            {
                jsonrpc = "2.0",
                id = 4,
                method = "tools/call",
                @params = new
                {
                    name = toolName,
                    arguments = new { workerId = "TRB-001" }
                }
            };

            var result = await ExtractToolResultAsync(await PostMcpAsync(payload, ct));
            Assert.True(result.GetProperty("found").GetBoolean());
        }

        // ── POST /mcp — tools/call: getContactInfo ────────────────────────────────

        [Fact]
        public async Task POST_Mcp_GetContactInfo_ReturnsAgencyContact()
        {
            var ct = TestContext.Current.CancellationToken;
            var toolName = await ResolveToolNameAsync("getContactInfo", ct);

            var payload = new
            {
                jsonrpc = "2.0",
                id = 5,
                method = "tools/call",
                @params = new
                {
                    name = toolName,
                    arguments = new { }
                }
            };

            var result = await ExtractToolResultAsync(await PostMcpAsync(payload, ct));
            Assert.Equal("Agencia Test", result.GetProperty("agencyName").GetString());
        }

        // ── GET / — server metadata ───────────────────────────────────────────────

        [Fact]
        public async Task GET_Root_ReturnsServerMetadata()
        {
            var ct = TestContext.Current.CancellationToken;
            var response = await _client.GetAsync("/", ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            Assert.Equal("IgnakeeAI MCP Labor Server", doc.RootElement.GetProperty("server").GetString());
        }

        // ── GET /health ───────────────────────────────────────────────────────────

        [Fact]
        public async Task GET_Health_Returns200()
        {
            var ct = TestContext.Current.CancellationToken;
            var response = await _client.GetAsync("/health", ct);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        private async Task<HttpResponseMessage> PostMcpAsync(object payload, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            // MCP SDK v1.0.0 requires Accept header with both values for Streamable HTTP.
            // With "application/json" only → JSON response. With both → SSE response.
            request.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");
            return await _client.SendAsync(request, ct);
        }

        private static async Task<JsonElement> ExtractToolResultAsync(HttpResponseMessage response)
        {
            var ct = TestContext.Current.CancellationToken;
            var body = await response.Content.ReadAsStringAsync(ct);

            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"HTTP {(int)response.StatusCode}. Body: {body}");

            var jsonLine = ExtractMcpJsonLine(body);

            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(jsonLine);
                root = doc.RootElement.Clone();
            }
            catch (JsonException e)
            {
                Assert.Fail($"MCP response is not valid JSON. Body: {body}");
                throw new JsonException($"Invalid MCP response: {e.Message}", e);
            }

            if (root.TryGetProperty("error", out var error))
            {
                Assert.Fail($"MCP error: {error.GetRawText()} | Body: {body}");
            }

            if (root.TryGetProperty("result", out var result) &&
                result.TryGetProperty("content", out var content) &&
                content.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in content.EnumerateArray())
                {
                    if (item.TryGetProperty("text", out var textProp))
                    {
                        var text = textProp.GetString();
                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return JsonDocument.Parse(text).RootElement.Clone();
                        }
                    }
                }
            }

            Assert.Fail($"Could not extract tool result from MCP response. Body: {body}");
            throw new InvalidOperationException("Unreachable");
        }

        private static string ExtractMcpJsonLine(string body)
        {
            if (!body.Contains("data:", StringComparison.Ordinal))
            {
                return body;
            }

            var dataLine = body
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(l => l.StartsWith("data:", StringComparison.Ordinal));

            Assert.True(dataLine is not null, $"Invalid SSE response. Body: {body}");
            return dataLine.Substring("data:".Length).Trim();
        }

        private async Task<string> ResolveToolNameAsync(string expectedName, CancellationToken ct)
        {
            if (_resolvedToolNames.TryGetValue(expectedName, out var cached))
                return cached!;

            var listPayload = new
            {
                jsonrpc = "2.0",
                id = Random.Shared.Next(1000, 9999),
                method = "tools/list",
                @params = new { }
            };

            var response = await PostMcpAsync(listPayload, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            var jsonLine = ExtractMcpJsonLine(body);

            using var doc = JsonDocument.Parse(jsonLine);
            var tools = doc.RootElement.GetProperty("result").GetProperty("tools");

            var mapped = ToolNameMap.TryGetValue(expectedName, out var snakeCaseName)
                ? snakeCaseName : expectedName;

            for (int i = 0; i < tools.GetArrayLength(); i++)
            {
                var name = tools[i].GetProperty("name").GetString()!;
                if (name.Equals(mapped, StringComparison.OrdinalIgnoreCase) ||
                    name.Equals(expectedName, StringComparison.OrdinalIgnoreCase))
                {
                    _resolvedToolNames[expectedName] = name;
                    return name;
                }
            }

            return mapped;
        }
    }
}
