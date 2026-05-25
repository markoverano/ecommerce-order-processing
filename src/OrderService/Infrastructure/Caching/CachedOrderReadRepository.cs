using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.Caching;
using ECommerceOrderProcessing.Infrastructure.Serialization;
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
public sealed class CachedOrderReadRepository : CachedReadRepository<OrderDto, OrderId>, IOrderReadRepository
{
    private readonly IOrderReadRepository _inner;

    public CachedOrderReadRepository(
        IOrderReadRepository inner,
        IDistributedCache cache,
        ILogger<CachedOrderReadRepository> logger)
        : base(cache, logger, id => $"order:{id.Value}", TimeSpan.FromMinutes(5))
    {
        _inner = inner;
    }

    public static string CacheKey(OrderId orderId) => $"order:{orderId.Value}";

    public Task<OrderDto?> GetByIdAsync(OrderId orderId, CancellationToken cancellationToken = default)
        => GetOrSetAsync(orderId, () => _inner.GetByIdAsync(orderId, cancellationToken), cancellationToken);

    public Task<PagedResult<OrderDto>> GetAllAsync(int page, int pageSize, Guid? customerId = null, CancellationToken cancellationToken = default)
        => _inner.GetAllAsync(page, pageSize, customerId, cancellationToken);
}
