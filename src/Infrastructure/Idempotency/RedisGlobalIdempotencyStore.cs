using Microsoft.Extensions.Caching.Distributed;

namespace ECommerceOrderProcessing.Infrastructure.Idempotency;

public sealed class RedisGlobalIdempotencyStore : IGlobalIdempotencyStore
{
    private readonly IDistributedCache _cache;

    public RedisGlobalIdempotencyStore(IDistributedCache cache)
    {
        _cache = cache;
    }

    public Task<string?> TryGetAsync(string key, CancellationToken cancellationToken = default)
        => _cache.GetStringAsync(key, cancellationToken);

    public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken = default)
        => _cache.SetStringAsync(key, value, new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl
        }, cancellationToken);
}
