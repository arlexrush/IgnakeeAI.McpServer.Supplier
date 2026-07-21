using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace IgnakeeAI.McpServer.Supplier.Tests.Integration;

public sealed class McpEndpointContractTests : IClassFixture<SupplierApiFactory>
{
    private readonly HttpClient _client;

    public McpEndpointContractTests(SupplierApiFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "mcp-test-key");
        _client.DefaultRequestHeaders.Add("Accept", "application/json, text/event-stream");
    }

    [Fact]
    public async Task Initialize_IsAuthorized()
    {
        var response = await PostAsync("initialize", new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "contract-test", version = "1.0" }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ToolsList_UsesOfficialPascalCaseNames()
    {
        var response = await PostAsync("tools/list", new { });
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("GetPrice", body);
        Assert.Contains("SearchAlternatives", body);
        Assert.Contains("CheckAvailability", body);
        Assert.Contains("GetBusinessHours", body);
    }

    private async Task<HttpResponseMessage> PostAsync(string method, object parameters) =>
        await _client.PostAsJsonAsync("/mcp", new
        {
            jsonrpc = "2.0",
            id = Guid.NewGuid(),
            method,
            @params = parameters
        });
}
