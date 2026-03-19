using Microsoft.AspNetCore.Authentication;

namespace IgnakeeAI.McpServer.Supplier.Api.Security
{
    public class ApiKeyAuthenticationOptions : AuthenticationSchemeOptions
    {
        public string ApiKey { get; set; }
    }
}
