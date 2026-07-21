using Microsoft.AspNetCore.Authentication;

namespace IgnakeeAI.McpServer.Supplier.Api.Security;

public sealed class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
{
    public string AdminApiKey { get; set; } = string.Empty;

    public IReadOnlyList<ApiKeyClientOptions> Clients { get; set; } = [];

}

public sealed class ApiKeyClientOptions
{
    public string ClientId { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = [];
}
