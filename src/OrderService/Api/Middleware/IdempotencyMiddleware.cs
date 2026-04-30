using System.Collections.Concurrent;
using System.Text;

namespace OrderService.Api.Middleware;

/// <summary>
/// Checks the X-Idempotency-Key header on mutating requests. If the key was already
/// processed, the cached response is replayed. Otherwise the request proceeds and the
/// response is cached for the duration of this process lifetime.
/// Phase 13 replaces this in-process store with a Redis-backed TTL store.
/// </summary>
public sealed class IdempotencyMiddleware
{
    private static readonly ConcurrentDictionary<string, CachedResponse> _store = new();

    private readonly RequestDelegate _next;
    private const string HeaderName = "X-Idempotency-Key";

    public IdempotencyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!HttpMethods.IsPost(context.Request.Method))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var keyValues) || string.IsNullOrWhiteSpace(keyValues))
        {
            await _next(context);
            return;
        }

        var key = keyValues.ToString();

        if (_store.TryGetValue(key, out var cached))
        {
            context.Response.StatusCode = cached.StatusCode;
            context.Response.ContentType = cached.ContentType;
            context.Response.Headers.Append("X-Idempotency-Replayed", "true");
            await context.Response.WriteAsync(cached.Body, Encoding.UTF8);
            return;
        }

        var originalBody = context.Response.Body;
        using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await _next(context);

            buffer.Position = 0;
            var responseBody = await new StreamReader(buffer).ReadToEndAsync();

            if (context.Response.StatusCode is >= 200 and < 300)
            {
                _store.TryAdd(key, new CachedResponse(
                    context.Response.StatusCode,
                    context.Response.ContentType ?? "application/json",
                    responseBody));
            }

            buffer.Position = 0;
            await buffer.CopyToAsync(originalBody);
        }
        finally
        {
            context.Response.Body = originalBody;
        }
    }

    private sealed record CachedResponse(int StatusCode, string ContentType, string Body);
}
