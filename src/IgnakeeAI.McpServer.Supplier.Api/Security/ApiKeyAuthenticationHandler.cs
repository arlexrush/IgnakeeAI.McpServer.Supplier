using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Security.Cryptography;
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
            if (string.IsNullOrEmpty(providedApiKey))
            {
                return Task.FromResult(AuthenticateResult.Fail("Clave de API no proporcionada."));
            }

            ApiKeyClientOptions? client = Options.Clients.FirstOrDefault(c =>
                KeysEqual(providedApiKey, c.ApiKey));
            var isAdmin = !string.IsNullOrEmpty(Options.AdminApiKey) &&
                KeysEqual(providedApiKey, Options.AdminApiKey);

            if (client is null && !isAdmin)
                return Task.FromResult(AuthenticateResult.Fail("Clave de API inválida."));

            var clientId = isAdmin ? "supplier-admin" : client!.ClientId;
            var claims = new List<Claim>
            {
                new(ClaimTypes.Name, clientId),
                new("client_id", clientId)
            };

            if (isAdmin)
            {
                claims.Add(new Claim("role", "supplier-admin"));
            }
            else
            {
                claims.AddRange(client!.Scopes
                    .Where(scope => !string.IsNullOrWhiteSpace(scope))
                    .Select(scope => new Claim("scope", scope)));
            }

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }

        private static bool KeysEqual(string providedKey, string expectedKey)
        {
            if (string.IsNullOrEmpty(expectedKey))
                return false;

            return CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(providedKey),
                System.Text.Encoding.UTF8.GetBytes(expectedKey));
        }
    }
}
