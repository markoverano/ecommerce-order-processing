using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.Serialization;
using ECommerceOrderProcessing.Shared.Errors;
using ECommerceOrderProcessing.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerceOrderProcessing.Infrastructure.RateLimiting;

/// <summary>
/// Redis-backed fixed-window rate limiting per client IP and route template.
/// Returns 429 with a <c>Retry-After</c> header when the limit is exceeded.
/// </summary>
public sealed class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RateLimitingOptions _options;
    private readonly ILogger<RateLimitingMiddleware> _logger;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IOptions<RateLimitingOptions> options,
        ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _options = options.Value;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint()?.DisplayName ?? context.Request.Path;
        var limit = GetLimit(context.Request.Path);
        if (limit is null)
        {
            await _next(context);
            return;
        }

        var clientKey = GetClientKey(context);
        var windowBucket = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / (long)limit.Window.TotalSeconds;
        var cacheKey = $"rate-limit:{endpoint}:{clientKey}:{windowBucket}";

        var cache = context.RequestServices.GetService(typeof(IDistributedCache)) as IDistributedCache;
        if (cache is null)
        {
            await _next(context);
            return;
        }

        var raw = await cache.GetStringAsync(cacheKey, context.RequestAborted);
        var current = raw is null ? 0 : int.Parse(raw);

        if (current >= limit.MaxRequests)
        {
            var retryAfter = (int)(limit.Window.TotalSeconds - DateTimeOffset.UtcNow.ToUnixTimeSeconds() % (long)limit.Window.TotalSeconds);
            _logger.LogWarning("Rate limit exceeded for {ClientKey} on {Path}", clientKey, context.Request.Path);

            context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.Response.ContentType = "application/json";
            context.Response.Headers.Append("Retry-After", retryAfter.ToString());
            context.Response.Headers.Append("X-RateLimit-Limit", limit.MaxRequests.ToString());
            context.Response.Headers.Append("X-RateLimit-Remaining", "0");
            context.Response.Headers.Append("X-RateLimit-Reset", (DateTimeOffset.UtcNow.AddSeconds(retryAfter)).ToUnixTimeSeconds().ToString());

            var body = JsonSerializer.Serialize(
                new ErrorResponse(ErrorCodes.RateLimitExceeded, $"Rate limit exceeded. Retry after {retryAfter} seconds."),
                InfrastructureJsonOptions.Default);
            await context.Response.WriteAsync(body, context.RequestAborted);
            return;
        }

        var newCount = current + 1;
        var remaining = limit.MaxRequests - newCount;
        context.Response.Headers.Append("X-RateLimit-Limit", limit.MaxRequests.ToString());
        context.Response.Headers.Append("X-RateLimit-Remaining", remaining.ToString());

        var ttl = limit.Window - TimeSpan.FromSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % (long)limit.Window.TotalSeconds);
        await cache.SetStringAsync(cacheKey, newCount.ToString(),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl },
            context.RequestAborted);

        await _next(context);
    }

    private EndpointRateLimit? GetLimit(PathString path)
    {
        foreach (var rule in _options.Rules)
        {
            if (path.StartsWithSegments(rule.PathPrefix, StringComparison.OrdinalIgnoreCase))
                return rule;
        }
        return null;
    }

    private static string GetClientKey(HttpContext context)
    {
        var forwarded = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        return forwarded?.Split(',')[0].Trim()
            ?? context.Connection.RemoteIpAddress?.ToString()
            ?? "unknown";
    }
}

public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";
    public IReadOnlyList<EndpointRateLimit> Rules { get; init; } = [];
}

public sealed class EndpointRateLimit
{
    public string PathPrefix { get; init; } = string.Empty;
    public int MaxRequests { get; init; }
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(1);
}
