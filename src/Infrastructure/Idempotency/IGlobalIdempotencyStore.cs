namespace ECommerceOrderProcessing.Infrastructure.Idempotency;

/// <summary>
/// Redis-backed store for command-level idempotency.
/// Prevents duplicate processing when the same command is delivered more than once via RabbitMQ or HTTP.
/// </summary>
public interface IGlobalIdempotencyStore
{
    Task<string?> TryGetAsync(string key, CancellationToken cancellationToken = default);
    Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default);
}
