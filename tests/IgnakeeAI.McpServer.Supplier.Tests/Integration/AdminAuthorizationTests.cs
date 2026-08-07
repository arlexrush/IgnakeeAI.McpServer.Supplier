using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace IgnakeeAI.McpServer.Supplier.Tests.Integration;

public sealed class AdminAuthorizationTests : IClassFixture<SupplierApiFactory>
{
    private readonly SupplierApiFactory _factory;

    public AdminAuthorizationTests(SupplierApiFactory factory) => _factory = factory;

    [Fact]
    public async Task McpClientKey_OnAdminEndpoint_Returns403()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "mcp-test-key");
        var response = await client.GetAsync("/admin/catalog/stats");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task McpClientKey_OnEcommerceSyncEndpoint_Returns403()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "mcp-test-key");
        var response = await client.PostAsync("/admin/sync/ecommerce", JsonContent.Create(new { }));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminKey_OnMcpEndpoint_Returns403()
    {
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "admin-test-key");
        var response = await client.PostAsync("/mcp", JsonContent.Create(new { }));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
