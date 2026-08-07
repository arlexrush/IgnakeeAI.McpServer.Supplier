using System.Net;
using System.Text;
using System.Text.Json;

namespace IgnakeeAI.McpServer.Supplier.Tests.Fakes
{
    /// <summary>
    /// Handler HTTP que intercepta peticiones al endpoint de inventario del ecommerce.
    /// Devuelve respuestas simuladas según la ruta y el código HTTP configurados.
    /// </summary>
    public sealed class EcommerceMockHttpHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public List<HttpRequestMessage> Requests { get; } = [];
        public List<string> RequestUrls { get; } = [];
        public List<string?> ApiKeyHeaders { get; } = [];

        /// <summary>Construye un handler con una función delegada de respuesta personalizada.</summary>
        public EcommerceMockHttpHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        /// <summary>Handler simple que devuelve siempre la misma respuesta para la ruta de producto individual.</summary>
        public static EcommerceMockHttpHandler ForProduct(HttpStatusCode status, string? body) =>
            new(request =>
            {
                var response = new HttpResponseMessage(status);
                if (body is not null)
                    response.Content = new StringContent(body, Encoding.UTF8, "application/json");
                return response;
            });

        /// <summary>
        /// Handler que devuelve respuestas de catálogo paginado.
        /// Llama a pageFactory con el número de página (1-based) para obtener la respuesta.
        /// </summary>
        public static EcommerceMockHttpHandler ForCatalog(
            Func<int, (HttpStatusCode status, string body)> pageFactory) =>
            new(request =>
            {
                var url = request.RequestUri?.ToString() ?? "";
                var page = ExtractQueryParam(url, "page");
                var pageNum = int.TryParse(page, out var p) ? p : 1;
                var (status, body) = pageFactory(pageNum);
                return new HttpResponseMessage(status)
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
            });

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            RequestUrls.Add(request.RequestUri?.ToString() ?? "");

            // Capturar el encabezado de la clave API
            var headerValue = request.Headers.TryGetValues("X-Api-Key", out var vals)
                ? vals.FirstOrDefault()
                : null;
            ApiKeyHeaders.Add(headerValue);

            return Task.FromResult(_handler(request));
        }

        private static string? ExtractQueryParam(string url, string name)
        {
            var query = url.Contains('?') ? url[(url.IndexOf('?') + 1)..] : "";
            foreach (var part in query.Split('&'))
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 &&
                    string.Equals(kv[0], name, StringComparison.OrdinalIgnoreCase))
                    return Uri.UnescapeDataString(kv[1]);
            }
            return null;
        }
    }
}
