using System.Net;
using System.Text;
using System.Text.Json;

namespace IgnakeeAI.McpServer.Supplier.Tests.Fakes
{
    /// <summary>
    /// Handler HTTP que intercepta las peticiones al SAP Service Layer
    /// y devuelve respuestas simuladas según la ruta invocada:
    ///   POST /Login  → respuesta de autenticación
    ///   GET  /Items  → catálogo de productos OData
    ///   POST /Logout → confirmación de cierre de sesión
    /// </summary>
    public class SapMockHttpHandler : HttpMessageHandler
    {
        private readonly string _loginResponse;
        private readonly string _itemsResponse;

        public int RequestCount { get; private set; }
        public List<string> RequestUrls { get; } = [];
        public List<string> RequestBodies { get; } = [];

        public SapMockHttpHandler(string loginResponse, string itemsResponse)
        {
            _loginResponse = loginResponse;
            _itemsResponse = itemsResponse;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUrls.Add(request.RequestUri?.ToString() ?? "");

            var body = request.Content is not null
                ? await request.Content.ReadAsStringAsync(cancellationToken)
                : "";
            RequestBodies.Add(body);

            var url = request.RequestUri?.AbsolutePath ?? "";
            var responseJson = url switch
            {
                var u when u.EndsWith("/Login") => _loginResponse,
                var u when u.Contains("/Items") => _itemsResponse,
                var u when u.EndsWith("/Logout") => SapFakeResponses.LogoutSuccess(),
                _ => _itemsResponse
            };

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        }
    }
}
