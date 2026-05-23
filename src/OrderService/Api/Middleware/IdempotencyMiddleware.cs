using System.Text;
using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.Idempotency;
using Microsoft.Extensions.DependencyInjection;

namespace OrderService.Api.Middleware;

/// <summary>
/// Checks the X-Idempotency-Key header on mutating requests. If the key was already
/// processed, the cached response is replayed. Otherwise the request proceeds and the
/// response is cached in Redis for 24 hours.
/// </summary>
public sealed class IdempotencyMiddleware
{
    private readonly RequestDelegate _next;
    private const string HeaderName = "X-Idempotency-Key";
    private static readonly TimeSpan Ttl = TimeSpan.FromHours(24);

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

        var key = $"http-idempotency:{keyValues}";
        var store = context.RequestServices.GetRequiredService<IGlobalIdempotencyStore>();

        var cached = await store.TryGetAsync(key, context.RequestAborted);
        if (cached is not null)
        {
            var entry = JsonSerializer.Deserialize<CachedResponse>(cached)!;
            context.Response.StatusCode = entry.StatusCode;
            context.Response.ContentType = entry.ContentType;
            context.Response.Headers.Append("X-Idempotency-Replayed", "true");
            await context.Response.WriteAsync(entry.Body, Encoding.UTF8);
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
                var entry = new CachedResponse(
                    context.Response.StatusCode,
                    context.Response.ContentType ?? "application/json",
                    responseBody);
                await store.SetAsync(key, JsonSerializer.Serialize(entry), Ttl, context.RequestAborted);
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
