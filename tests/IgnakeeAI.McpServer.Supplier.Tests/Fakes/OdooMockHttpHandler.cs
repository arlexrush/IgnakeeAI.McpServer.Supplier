using System.Net;
using System.Text;
using System.Text.Json;

namespace IgnakeeAI.McpServer.Supplier.Tests.Fakes
{
    /// <summary>
    /// Handler HTTP que intercepta las peticiones JSON-RPC dirigidas a Odoo
    /// y devuelve respuestas simuladas según el servicio/método invocado.
    /// 
    /// Inspecciona el payload JSON-RPC para distinguir entre:
    ///   - common/authenticate → respuesta de autenticación
    ///   - object/execute_kw  → respuesta de search_read (productos)
    /// </summary>
    public class OdooMockHttpHandler : HttpMessageHandler
    {
        private readonly string _authResponse;
        private readonly string _searchReadResponse;

        /// <summary>Contador de peticiones recibidas (útil para asserts).</summary>
        public int RequestCount { get; private set; }

        /// <summary>Última URL solicitada.</summary>
        public string? LastRequestUrl { get; private set; }

        /// <summary>Últimos payloads enviados (para inspección en tests).</summary>
        public List<string> RequestBodies { get; } = [];

        public OdooMockHttpHandler(string authResponse, string searchReadResponse)
        {
            _authResponse = authResponse;
            _searchReadResponse = searchReadResponse;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUrl = request.RequestUri?.ToString();

            var body = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : "";
            RequestBodies.Add(body);

            // Determinar qué tipo de llamada es inspeccionando el payload JSON-RPC
            var responseJson = ResolveResponse(body);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }

        private string ResolveResponse(string requestBody)
        {
            try
            {
                var doc = JsonDocument.Parse(requestBody);
                var root = doc.RootElement;

                if (root.TryGetProperty("params", out var p) &&
                    p.TryGetProperty("service", out var service))
                {
                    return service.GetString() switch
                    {
                        "common" => _authResponse,
                        "object" => _searchReadResponse,
                        _ => _searchReadResponse
                    };
                }
            }
            catch
            {
                // Si no se puede parsear, devolver search_read por defecto
            }

            return _searchReadResponse;
        }
    }
}
