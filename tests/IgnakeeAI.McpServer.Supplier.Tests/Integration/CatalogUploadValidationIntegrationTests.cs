using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IgnakeeAI.McpServer.Supplier.Tests.Integration;

public sealed class CatalogUploadValidationIntegrationTests : IClassFixture<SupplierApiFactory>, IAsyncLifetime
{
    private readonly SupplierApiFactory _factory;
    private readonly HttpClient _client;
    private IServiceScope? _scope;

    public CatalogUploadValidationIntegrationTests(SupplierApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-Api-Key", "admin-test-key");
    }

    public async ValueTask InitializeAsync() => _scope = await _factory.SeedDatabaseAsync();

    public ValueTask DisposeAsync()
    {
        _scope?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task POST_AdminSyncCsv_WithUnexpectedContentType_Returns400()
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes("ItemCode;Description"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(file, "file", "catalog.csv");

        var response = await _client.PostAsync("/admin/sync/csv", form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task POST_AdminSyncExcel_WithInvalidOpenXmlPackage_Returns400()
    {
        using var form = new MultipartFormDataContent();
        using var file = new ByteArrayContent(Encoding.UTF8.GetBytes("not an xlsx archive"));
        file.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        form.Add(file, "file", "catalog.xlsx");

        var response = await _client.PostAsync("/admin/sync/excel", form, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
