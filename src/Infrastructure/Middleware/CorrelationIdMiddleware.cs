using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ECommerceOrderProcessing.Infrastructure.Middleware;

/// <summary>
/// Reads X-Correlation-ID from the request header (or generates one) and propagates it
/// on the response. Stored in HttpContext.Items so handlers can access it without ambient state.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    private const string HeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Headers.TryGetValue(HeaderName, out var correlationIdValue)
            || !Guid.TryParse(correlationIdValue, out var correlationId))
        {
            correlationId = Guid.NewGuid();
        }

        context.Items[HeaderName] = correlationId;
        context.Response.Headers[HeaderName] = correlationId.ToString();

        using (_logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await _next(context);
        }
    }
}
