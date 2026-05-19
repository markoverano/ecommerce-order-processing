using System.Text.Json;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OrderService.Application.DTOs;
using OrderService.Application.Repositories;

namespace OrderService.Infrastructure.Caching;

/// <summary>
/// Decorator that adds a 5-minute Redis cache in front of the EF Core read repository.
/// Cache is keyed on order ID and evicted whenever the write-side repository persists a change.
/// </summary>
public sealed class CachedOrderReadRepository : IOrderReadRepository
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly IOrderReadRepository _inner;
    private readonly IDistributedCache _cache;
    private readonly ILogger<CachedOrderReadRepository> _logger;

    public CachedOrderReadRepository(
        IOrderReadRepository inner,
        IDistributedCache cache,
        ILogger<CachedOrderReadRepository> logger)
    {
        _inner = inner;
        _cache = cache;
        _logger = logger;
    }

    public static string CacheKey(OrderId orderId) => $"order:{orderId.Value}";

    public async Task<OrderDto?> GetByIdAsync(OrderId orderId, CancellationToken cancellationToken = default)
    {
        var key = CacheKey(orderId);

        var cached = await _cache.GetStringAsync(key, cancellationToken);
        if (cached is not null)
        {
            _logger.LogDebug("Cache hit for order {OrderId}", orderId.Value);
            return JsonSerializer.Deserialize<OrderDto>(cached, _jsonOptions);
        }

        var dto = await _inner.GetByIdAsync(orderId, cancellationToken);
        if (dto is not null)
        {
            await _cache.SetStringAsync(
                key,
                JsonSerializer.Serialize(dto, _jsonOptions),
                new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = CacheTtl },
                cancellationToken);
        }

        return dto;
    }

    public Task<PagedResult<OrderDto>> GetAllAsync(int page, int pageSize, Guid? customerId = null, CancellationToken cancellationToken = default)
        => _inner.GetAllAsync(page, pageSize, customerId, cancellationToken);
}
