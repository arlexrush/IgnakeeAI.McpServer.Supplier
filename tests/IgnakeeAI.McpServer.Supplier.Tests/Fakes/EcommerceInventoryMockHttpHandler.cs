using System.Net;
using System.Text;

namespace IgnakeeAI.McpServer.Supplier.Tests.Fakes;

public sealed class EcommerceInventoryMockHttpHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

    public EcommerceInventoryMockHttpHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
    {
        _handler = handler;
    }

    public List<EcommerceInventoryRequestRecord> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add(new EcommerceInventoryRequestRecord(
            request.Method.Method,
            request.RequestUri?.ToString() ?? string.Empty,
            request.Headers.ToDictionary(header => header.Key, header => string.Join(",", header.Value)),
            body));

        return await _handler(request, cancellationToken);
    }

    public static HttpResponseMessage Json(HttpStatusCode statusCode, string json) =>
        new(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
}

public sealed record EcommerceInventoryRequestRecord(
    string Method,
    string Url,
    IReadOnlyDictionary<string, string> Headers,
    string Body);
