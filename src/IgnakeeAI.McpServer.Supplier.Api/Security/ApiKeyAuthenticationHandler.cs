using Azure.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;

namespace IgnakeeAI.McpServer.Supplier.Api.Security
{
    public class ApiKeyAuthenticationHandler : AuthenticationHandler<ApiKeyAuthenticationOptions>
    {
        public ApiKeyAuthenticationHandler(
            IOptionsMonitor<ApiKeyAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeaderValues))
            {
                return Task.FromResult(AuthenticateResult.Fail("No se proporcionó la clave de API."));
            }

            var providedApiKey = apiKeyHeaderValues.FirstOrDefault();
            if (providedApiKey == null || providedApiKey != Options.ApiKey)
            {
                return Task.FromResult(AuthenticateResult.Fail("Clave de API inválida."));
            }

            var claims = new[] { new Claim(ClaimTypes.Name, "Admin") };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
