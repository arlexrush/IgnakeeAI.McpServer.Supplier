using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;
using IgnakeeAI.McpServer.Supplier.Application.Contracts;

namespace IgnakeeAI.McpServer.Supplier.Tests.Integration
{
    /// <summary>
    /// Pruebas de integración para el endpoint MCP principal: POST /mcp.
    /// Verifica el flujo completo: recepción del mensaje MCP → tool dispatch
    /// → CatalogSearchService → BD → serialización de respuesta.
    ///
    /// El protocolo MCP sobre HTTP transporta mensajes JSON-RPC 2.0.
    /// El endpoint responde con SSE (text/event-stream) o JSON según el cliente.
    /// Se usa el header Accept: application/json para forzar respuesta síncrona.
    /// </summary>
    public class McpEndpointIntegrationTests : IClassFixture<SupplierApiFactory>, IAsyncLifetime
    {
        private static readonly Dictionary<string, string> ToolNameMap = new(StringComparer.Ordinal)
        {
            [SupplierMcpToolNames.GetPrice] = SupplierMcpToolNames.GetPrice,
            [SupplierMcpToolNames.SearchAlternatives] = SupplierMcpToolNames.SearchAlternatives,
            [SupplierMcpToolNames.CheckAvailability] = SupplierMcpToolNames.CheckAvailability,
            [SupplierMcpToolNames.GetBusinessHours] = SupplierMcpToolNames.GetBusinessHours
        };
        private readonly ConcurrentDictionary<string, string> _resolvedToolNames = new(StringComparer.OrdinalIgnoreCase);
        private readonly SupplierApiFactory _factory;
        private readonly HttpClient _client;
        private IServiceScope? _scope;

        public McpEndpointIntegrationTests(SupplierApiFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
            _client.DefaultRequestHeaders.Add("X-Api-Key", "mcp-test-key");
            // _client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");
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
            // Arrange — mensaje de inicialización del protocolo MCP
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
                    clientInfo = new { name = "integration-test", version = "1.0" }
                }
            };

            // Act
            var response = await PostMcpAsync(payload, ct);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // ── POST /mcp — tools/list ────────────────────────────────────────────────

        [Fact]
        public async Task POST_Mcp_ToolsList_ReturnsAllFourTools()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var payload = new
            {
                jsonrpc = "2.0",
                id = 2,
                method = "tools/list",
                @params = new { }
            };

            // Act
            var response = await PostMcpAsync(payload, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            // Assert — el endpoint MCP respondió con éxito y contiene las tool names
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains(SupplierMcpToolNames.GetPrice, body);
            Assert.Contains(SupplierMcpToolNames.SearchAlternatives, body);
            Assert.Contains(SupplierMcpToolNames.CheckAvailability, body);
            Assert.Contains(SupplierMcpToolNames.GetBusinessHours, body);
        }

        // ── POST /mcp — tools/call: getPrice ────────────────────────────────────

        [Fact]
        public async Task POST_Mcp_GetPrice_ByItemCode_ReturnsFoundTrue()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var payload = BuildToolCallPayload(SupplierMcpToolNames.GetPrice, new
            {
                itemCode = "CEM-STD",
                itemDescription = "cemento estándar"
            });

            // Act
            var response = await PostMcpAsync(payload, ct);
            var toolResult = await ExtractToolResultAsync(response);

            // Assert
            Assert.True(toolResult.GetProperty("found").GetBoolean());
            Assert.Equal("CEM-STD", toolResult.GetProperty("itemCode").GetString());
            Assert.Equal(5.00m, toolResult.GetProperty("unitPrice").GetDecimal());
            Assert.Equal("EUR", toolResult.GetProperty("currency").GetString());
        }

        [Fact]
        public async Task POST_Mcp_GetPrice_ByDescription_ReturnsFoundTrue()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var toolName = await ResolveToolNameAsync(SupplierMcpToolNames.GetPrice, ct);
            var payload = BuildToolCallPayload(toolName, new
            {
                itemDescription = "cemento premium estructural",
                itemCode = (string?)null
            });

            // Act
            var response = await PostMcpAsync(payload, ct);
            var toolResult = await ExtractToolResultAsync(response);

            // Assert
            Assert.True(toolResult.GetProperty("found").GetBoolean());
            Assert.Equal("CEM-PREMIUM", toolResult.GetProperty("itemCode").GetString());
        }

        [Fact]
        public async Task POST_Mcp_GetPrice_OnSaleProduct_ReturnsEffectivePrice()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var payload = BuildToolCallPayload(SupplierMcpToolNames.GetPrice, new
            {
                itemDescription = "cemento oferta",
                itemCode = (string?)null
            });

            // Act
            var response = await PostMcpAsync(payload, ct);
            var toolResult = await ExtractToolResultAsync(response);

            // Assert
            Assert.True(toolResult.GetProperty("found").GetBoolean());
            Assert.True(toolResult.GetProperty("isOnSale").GetBoolean());
            Assert.Equal(4.50m, toolResult.GetProperty("unitPrice").GetDecimal());
            Assert.Equal(6.00m, toolResult.GetProperty("originalPrice").GetDecimal());
        }

        [Fact]
        public async Task POST_Mcp_GetPrice_NotFound_ReturnsFoundFalse()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var payload = BuildToolCallPayload(SupplierMcpToolNames.GetPrice, new
            {
                itemDescription = "producto inexistente xyz123",
                itemCode = (string?)null
            });

            // Act
            var response = await PostMcpAsync(payload, ct);
            var toolResult = await ExtractToolResultAsync(response);

            // Assert
            Assert.False(toolResult.GetProperty("found").GetBoolean());
        }

        // ── POST /mcp — tools/call: searchAlternatives ───────────────────────────

        [Fact]
        public async Task POST_Mcp_SearchAlternatives_Cheaper_ReturnsAlternatives()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var payload = BuildToolCallPayload(SupplierMcpToolNames.SearchAlternatives, new
            {
                itemDescription = "cemento premium",
                category = "cementos",
                criteria = "cheaper",
                maxResults = 5
            });

            // Act
            var response = await PostMcpAsync(payload, ct);
            var toolResult = await ExtractToolResultAsync(response);

            // Assert
            Assert.True(toolResult.GetProperty("found").GetBoolean());
            Assert.True(toolResult.GetProperty("count").GetInt32() >= 1);

            var first = toolResult.GetProperty("alternatives").EnumerateArray().First();
            Assert.True(first.GetProperty("unitPrice").GetDecimal() < 8.00m);
        }

        [Fact]
        public async Task POST_Mcp_SearchAlternatives_OnSale_ReturnsOnlyOnSaleProducts()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var payload = BuildToolCallPayload(SupplierMcpToolNames.SearchAlternatives, new
            {
                itemDescription = "cemento",
                category = "cementos",
                criteria = "onSale",
                maxResults = 5
            });

            // Act
            var response = await PostMcpAsync(payload, ct);
            var toolResult = await ExtractToolResultAsync(response);

            // Assert
            Assert.True(toolResult.GetProperty("found").GetBoolean());

            var alternatives = toolResult.GetProperty("alternatives").EnumerateArray().ToList();
            Assert.NotEmpty(alternatives);
            Assert.All(alternatives, alt => Assert.True(alt.GetProperty("isOnSale").GetBoolean()));
        }

        // ── POST /mcp — tools/call: checkAvailability ────────────────────────────

        [Fact]
        public async Task POST_Mcp_CheckAvailability_ExistingProduct_ReturnsFoundTrue()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var toolName = await ResolveToolNameAsync(SupplierMcpToolNames.CheckAvailability, ct);
            var payload = BuildToolCallPayload(toolName, new
            {
                itemCode = "ACE-001"
            });

            // Act
            var response = await PostMcpAsync(payload, ct); // El endpoint MCP responde con SSE, pero el SDK ModelContextProtocol maneja eso internamente y devuelve el resultado final como JSON.
            var toolResult = await ExtractToolResultAsync(response);

            // Assert
            Assert.True(
                toolResult.TryGetProperty("found", out var foundProp),
                $"La respuesta no contiene 'found'. Payload: {toolResult.GetRawText()}");

            Assert.True(foundProp.GetBoolean());

            Assert.True(
                toolResult.TryGetProperty("availableStock", out var stockProp),
                $"La respuesta no contiene 'availableStock'. Payload: {toolResult.GetRawText()}");

            Assert.True(stockProp.GetInt32() > 0);
        }

        [Fact]
        public async Task POST_Mcp_CheckAvailability_UnknownCode_ReturnsFoundFalse()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var payload = BuildToolCallPayload(SupplierMcpToolNames.CheckAvailability, new
            {
                itemCode = "INEXISTENTE-999"
            });

            // Act
            var response = await PostMcpAsync(payload, ct);
            var toolResult = await ExtractToolResultAsync(response);

            // Assert
            Assert.False(toolResult.GetProperty("found").GetBoolean());
        }

        // ── POST /mcp — tools/call: getBusinessHours ─────────────────────────────

        [Fact]
        public async Task POST_Mcp_GetBusinessHours_ReturnsSupplierContact()
        {
            // Arrange
            var ct = TestContext.Current.CancellationToken;
            var payload = BuildToolCallPayload(SupplierMcpToolNames.GetBusinessHours, new { });

            // Act
            var response = await PostMcpAsync(payload, ct);
            var toolResult = await ExtractToolResultAsync(response);

            // Assert — la factory configura variables de entorno de proveedor
            Assert.NotNull(toolResult.GetProperty("vendorName").GetString());
            Assert.NotNull(toolResult.GetProperty("hours").GetString());
        }

        // ── Helpers ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Envía una request al endpoint MCP con los headers exactos que requiere
        /// el SDK ModelContextProtocol.AspNetCore v1.0.0 (Streamable HTTP 2025-11-25).
        ///
        /// El endpoint POST /mcp declara AcceptsMetadata("application/json"), por lo
        /// que el Content-Type debe ser exactamente "application/json".
        /// El header Accept negocia si la respuesta es SSE (text/event-stream) o JSON.
        /// Usar PostAsJsonAsync con DefaultRequestHeaders globales provoca que el SDK
        /// rechace la request con 400 BadRequest por negociación de contenido incorrecta.
        /// </summary>
        private async Task<HttpResponseMessage> PostMcpAsync(object payload, CancellationToken ct)
        {
            var json = JsonSerializer.Serialize(payload);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            // El SDK MCP v1.0.0 requiere estos dos valores en Accept para Streamable HTTP.
            // Con solo "application/json" responde 200+JSON (sin SSE).
            // Con "text/event-stream" o ambos responde 200+SSE.
            request.Headers.TryAddWithoutValidation("Accept", "application/json, text/event-stream");

            return await _client.SendAsync(request, ct);
        }

        private static string NormalizeToolName(string toolName) =>
            ToolNameMap.TryGetValue(toolName, out var mapped) ? mapped : toolName;

        /// <summary>
        /// Responsable de construir el payload JSON-RPC para llamar a una tool específica.
        /// </summary>
        /// <param name="toolName"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        private static object BuildToolCallPayload(string toolName, object arguments) => new
        {
            jsonrpc = "2.0",
            id = Random.Shared.Next(1000, 9999),
            method = "tools/call",
            @params = new
            {
                name = NormalizeToolName(toolName),
                arguments
            }
        };

        /// <summary>
        /// Extrae el JsonElement con el resultado de la tool del mensaje de respuesta MCP.
        /// El SDK ModelContextProtocol devuelve los resultados como content[0].text (JSON embebido).
        /// Si la respuesta es SSE, extrae el último evento data: completo.
        /// </summary>
        private static async Task<JsonElement> ExtractToolResultAsync(HttpResponseMessage response)
        {
            var ct = TestContext.Current.CancellationToken;
            var body = await response.Content.ReadAsStringAsync(ct);
                       
            // Assert con diagnóstico: muestra el cuerpo real del servidor al fallar,
            // en lugar del inútil "Expected: OK / Actual: BadRequest"
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"HTTP {(int)response.StatusCode} {response.StatusCode}. " +
                $"Content-Type: {response.Content.Headers.ContentType}. Body: {body}");

            // Extraer la línea del evento SSE si aplica
            var jsonLine = ExtractMcpJsonLine(body);
            //if (body.Contains("data:"))
            //{
            //    jsonLine = body
            //        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            //        .Last(l => l.StartsWith("data:"))
            //        .Substring("data:".Length)
            //        .Trim();
            //}
            //else
            //{
            //    jsonLine = body;
            //}

            JsonElement root;
            try
            {
                using var doc = JsonDocument.Parse(jsonLine);
                root = doc.RootElement.Clone();
            }
            catch (JsonException e)
            {
                Assert.Fail($"La respuesta MCP no es JSON válido. Body: {body}");
                throw new JsonException($"Respuesta MCP no válida: {e.Message}", e);
            }

            // Si MCP devolvió error explícito, falla con detalle
            if (root.TryGetProperty("error", out var error))
            {
                Assert.Fail($"MCP error: {error.GetRawText()} | Body: {body}");
            }

            // result -> content[*] puede traer text o json según transporte/versión
            //if (root.TryGetProperty("result", out var result) &&
            //    result.TryGetProperty("content", out var content) &&
            //    content.ValueKind == JsonValueKind.Array)
            //{
            //    foreach (var item in content.EnumerateArray())
            //    {
            //        if (item.TryGetProperty("json", out var jsonPayload))
            //        {
            //            return jsonPayload.Clone();
            //        }

            //        if (item.TryGetProperty("text", out var textPayload))
            //        {
            //            var text = textPayload.GetString();
            //            if (!string.IsNullOrWhiteSpace(text))
            //            {
            //                try
            //                {
            //                    return JsonDocument.Parse(text).RootElement.Clone();
            //                }
            //                catch (JsonException)
            //                {
            //                    // Si no es JSON, seguimos buscando otro item válido.
            //                }
            //            }
            //        }
            //    }
            //}

            // result -> content[*] puede traer text o json según transporte/versión
            if (root.TryGetProperty("result", out var result))
            {
                // MCP puede responder OK HTTP pero con error de tool en result.isError=true
                if (result.TryGetProperty("isError", out var isError) &&
                    isError.ValueKind == JsonValueKind.True)
                {
                    var message = result.TryGetProperty("content", out var errorContent) &&
                        errorContent.ValueKind == JsonValueKind.Array
                        ? string.Join(
                            " | ",
                            errorContent.EnumerateArray()
                                .Select(item =>
                                    item.TryGetProperty("text", out var textProp) &&
                                    textProp.ValueKind == JsonValueKind.String
                                        ? textProp.GetString()
                                        : item.GetRawText())
                                .Where(s => !string.IsNullOrWhiteSpace(s)))
                        : result.GetRawText();

                    Assert.Fail($"MCP tool error: {message} | Body: {body}");
                }

                if (result.TryGetProperty("content", out var content) &&
                    content.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in content.EnumerateArray())
                    {
                        if (item.TryGetProperty("json", out var jsonPayload))
                        {
                            return jsonPayload.Clone();
                        }

                        if (item.TryGetProperty("text", out var textPayload))
                        {
                            var text = textPayload.GetString();
                            if (TryParseJsonElement(text, out var parsed))
                            {
                                return parsed;
                            }
                        }
                    }
                }
            }

            // Si la respuesta es directamente el objeto tool
            return root;
        }

        private static bool TryParseJsonElement(string? text, out JsonElement parsed)
        {
            parsed = default;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var trimmed = text.TrimStart();
            var first = trimmed[0];
            if (first != '{' && first != '[')
            {
                return false;
            }

            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                parsed = doc.RootElement.Clone();
                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }

        /// <summary>
        /// Responsable de resolver el nombre real de la tool en el servidor MCP, dado un nombre esperado.
        /// </summary>
        /// <param name="expectedName">Nombre esperado de la tool.</param>
        /// <param name="ct">Token de cancelación.</param>
        /// <returns>Nombre real de la tool en el servidor MCP.</returns>
        private async Task<string> ResolveToolNameAsync(string expectedName, CancellationToken ct)
        {
            if (_resolvedToolNames.TryGetValue(expectedName, out var cached)) // Evita llamar a tools/list repetidamente para cada test, ya que el servidor MCP no suele cambiar las tools en caliente.
            {
                return cached!; // Cachea el resultado para acelerar tests posteriores que usen la misma tool.
            }

            var listPayload = new
            {
                jsonrpc = "2.0",
                id = Random.Shared.Next(1000, 9999),
                method = "tools/list",
                @params = new { }
            };

           
            var response = await PostMcpAsync(listPayload, ct); // Reutiliza el helper de POST para enviar la request MCP con los headers correctos.
                       
            var body = await response.Content.ReadAsStringAsync(ct); // El cuerpo de respuesta puede ser JSON directo o SSE con JSON embebido, así que lo mostramos completo en el mensaje de error para diagnóstico.

            // Assert con diagnóstico: muestra el cuerpo real del servidor al fallar, en lugar del inútil "Expected: OK / Actual: BadRequest"
            Assert.True(
                response.StatusCode == HttpStatusCode.OK,
                $"tools/list devolvió {(int)response.StatusCode} {response.StatusCode}. Body: {body}");

            var jsonLine = ExtractMcpJsonLine(body);

            JsonElement root;

            try
            {
                using var doc = JsonDocument.Parse(jsonLine); // El formato típico es { result: { tools: [ { name: "getPrice" }, ... ] } }, pero el servidor MCP podría devolverlo de forma diferente, así que buscamos de forma flexible.
                root = doc.RootElement.Clone(); // El servidor MCP podría devolver las tools directamente en el body sin envolver en "result", o con una estructura diferente, así que buscamos de forma flexible.
            }
            catch (JsonException e)
            {
                Assert.Fail($"tools/list no devolvió un JSON válido. Body: {body}");
                throw new JsonException($"Respuesta no es JSON válido: {e.Message}", e);
            }

            if (root.TryGetProperty("error", out var error))
            {
                Assert.Fail($"MCP error en tools/list: {error.GetRawText()} | Body: {body}");
            }

            var names = new List<string>();

            var foundTools = root.TryGetProperty("result", out var resultX) &&
                resultX.TryGetProperty("tools", out var toolsX) &&
                toolsX.ValueKind == JsonValueKind.Array;

            // MCP típico: result.tools[*].name
            if (root.TryGetProperty("result", out var result) &&
                result.TryGetProperty("tools", out var tools) &&
                tools.ValueKind == JsonValueKind.Array) // El servidor MCP podría devolver las tools directamente en el body sin envolver en "result", o con una estructura diferente, así que buscamos de forma flexible.
            {
                foreach (var t in tools.EnumerateArray())
                {
                    if (t.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String)
                    {
                        names.Add(n.GetString()!);
                    }
                }
            }

            Assert.NotEmpty(names);

            var normalizedExpected = NormalizeToolName(expectedName);

            // Intentamos encontrar una coincidencia exacta ignorando mayúsculas, o una coincidencia flexible para "checkAvailability" y "getBusinessHours" que podrían tener nombres diferentes en el servidor MCP.
            var match = names.FirstOrDefault(n => string.Equals(n, normalizedExpected, StringComparison.OrdinalIgnoreCase))
                ?? names.FirstOrDefault(n => string.Equals(n, expectedName, StringComparison.OrdinalIgnoreCase));

            Assert.True(match is not null, $"No se encontró tool '{expectedName}'. Tools disponibles: {string.Join(", ", names)}");

            _resolvedToolNames[expectedName] = match!;
            return match!;
        }

        /// <summary>
        /// Responsable de extraer la línea JSON del cuerpo de respuesta del servidor MCP, manejando tanto respuestas SSE como JSON directo.
        /// </summary>
        /// <param name="body">El cuerpo de la respuesta del servidor MCP.</param>
        /// <returns>La línea JSON extraída del cuerpo de la respuesta.</returns>
        private static string ExtractMcpJsonLine(string body)
        {
            if (!body.Contains("data:", StringComparison.Ordinal))
            {
                return body;
            }

            var dataLine = body
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .LastOrDefault(l => l.StartsWith("data:", StringComparison.Ordinal));

            Assert.True(dataLine is not null, $"Respuesta SSE inválida. Body: {body}");

            return dataLine.Substring("data:".Length).Trim();
        }

    }
}
