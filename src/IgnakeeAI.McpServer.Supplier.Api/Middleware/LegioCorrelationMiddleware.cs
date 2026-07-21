using System.Diagnostics;

namespace IgnakeeAI.McpServer.Supplier.Api.Middleware;

public sealed record LegioCorrelationContext(
    Guid RequestId,
    Guid TraceId,
    Guid? ProjectId,
    Guid? RoutingQuoteId,
    string? ContractVersion);

/// <summary>
/// Valida y propaga los identificadores de trazabilidad enviados por Legio.
/// Nunca registra headers de autenticación ni secretos.
/// </summary>
public sealed class LegioCorrelationMiddleware
{
    private const string ContextItemKey = "LegioCorrelationContext";
    private readonly RequestDelegate _next;
    private readonly ILogger<LegioCorrelationMiddleware> _logger;

    public LegioCorrelationMiddleware(
        RequestDelegate next,
        ILogger<LegioCorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext httpContext)
    {
        string? requestError = null;
        string? traceError = null;
        string? projectError = null;
        string? routingError = null;

        if (!TryReadGuid(httpContext, "X-Legio-Request-Id", out var requestId, out requestError) ||
            !TryReadGuid(httpContext, "X-Legio-Trace-Id", out var traceId, out traceError) ||
            !TryReadOptionalGuid(httpContext, "X-Legio-Project-Id", out var projectId, out projectError) ||
            !TryReadOptionalGuid(httpContext, "X-Legio-Routing-Quote-Id", out var routingQuoteId, out routingError))
        {
            var error = requestError ?? traceError ?? projectError ?? routingError ?? "Headers de correlación no válidos.";
            httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            httpContext.Response.ContentType = "application/problem+json";
            await httpContext.Response.WriteAsJsonAsync(new
            {
                type = "https://www.rfc-editor.org/rfc/rfc9110#name-400-bad-request",
                title = "Invalid Legio correlation header",
                status = StatusCodes.Status400BadRequest,
                detail = error
            });
            return;
        }

        var context = new LegioCorrelationContext(
            requestId ?? Guid.NewGuid(),
            traceId ?? Guid.NewGuid(),
            projectId,
            routingQuoteId,
            HeaderValue(httpContext, "X-Legio-Contract-Version"));

        httpContext.Items[ContextItemKey] = context;
        httpContext.Response.Headers["X-Legio-Request-Id"] = context.RequestId.ToString();
        httpContext.Response.Headers["X-Legio-Trace-Id"] = context.TraceId.ToString();

        var stopwatch = Stopwatch.StartNew();
        using var scope = _logger.BeginScope(new Dictionary<string, object?>
        {
            ["requestId"] = context.RequestId,
            ["traceId"] = context.TraceId,
            ["projectId"] = context.ProjectId,
            ["routingQuoteId"] = context.RoutingQuoteId,
            ["contractVersion"] = context.ContractVersion
        });

        try
        {
            await _next(httpContext);
        }
        finally
        {
            stopwatch.Stop();
            _logger.LogInformation(
                "Legio request completed: {RequestPath} {StatusCode} in {DurationMs} ms",
                httpContext.Request.Path,
                httpContext.Response.StatusCode,
                stopwatch.Elapsed.TotalMilliseconds);
        }
    }

    private static bool TryReadGuid(
        HttpContext context,
        string headerName,
        out Guid? value,
        out string? error)
    {
        var raw = HeaderValue(context, headerName);
        if (string.IsNullOrWhiteSpace(raw))
        {
            value = null;
            error = null;
            return true;
        }

        if (Guid.TryParse(raw, out var parsed))
        {
            value = parsed;
            error = null;
            return true;
        }

        value = null;
        error = $"El header {headerName} debe contener un GUID válido.";
        return false;
    }

    private static bool TryReadOptionalGuid(
        HttpContext context,
        string headerName,
        out Guid? value,
        out string? error) => TryReadGuid(context, headerName, out value, out error);

    private static string? HeaderValue(HttpContext context, string headerName) =>
        context.Request.Headers.TryGetValue(headerName, out var values)
            ? values.FirstOrDefault()
            : null;
}
