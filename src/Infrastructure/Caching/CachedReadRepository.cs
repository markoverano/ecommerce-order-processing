using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;

namespace ECommerceOrderProcessing.Infrastructure.Caching;

/// <summary>
/// Generic caching decorator for read repositories.
/// Accepts a key factory so each service can define its own cache key format.
/// </summary>
public abstract class CachedReadRepository<TDto, TId>
{
    private readonly IDistributedCache _cache;
    private readonly ILogger _logger;
    private readonly Func<TId, string> _keyFactory;
    private readonly TimeSpan _ttl;

    protected CachedReadRepository(
        IDistributedCache cache,
        ILogger logger,
        Func<TId, string> keyFactory,
        TimeSpan ttl)
    {
        _cache = cache;
        _logger = logger;
        _keyFactory = keyFactory;
        _ttl = ttl;
    }

    protected async Task<TDto?> GetOrSetAsync(
        TId id,
        Func<Task<TDto?>> fetch,
        CancellationToken cancellationToken = default)
    {
        var key = _keyFactory(id);

        var cached = await _cache.GetStringAsync(key, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for key {Key}", key);
            return JsonSerializer.Deserialize<TDto>(cached, InfrastructureJsonOptions.Default);
        }

        var dto = await fetch();
        if (dto is not null)
        {
            await _cache.SetStringAsync(
                key,
                JsonSerializer.Serialize(dto, InfrastructureJsonOptions.Default),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _ttl },
                cancellationToken);
        }

        return dto;
    }
}
