using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace IgnakeeAI.McpServer.Supplier.Tests.Integration;

public sealed class McpAuthenticationTests : IClassFixture<SupplierApiFactory>
{
    private readonly SupplierApiFactory _factory;

    public McpAuthenticationTests(SupplierApiFactory factory) => _factory = factory;

    [Fact]
    public async Task MissingApiKey_Returns401()
    {
        using var client = _factory.CreateClient();
        var response = await client.PostAsync("/mcp", JsonContent.Create(new { }));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task InvalidApiKey_Returns401()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "invalid-test-key");
        var response = await client.PostAsync("/mcp", JsonContent.Create(new { }));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
