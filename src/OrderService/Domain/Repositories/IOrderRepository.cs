using ECommerceOrderProcessing.Shared.ValueObjects;
using OrderService.Domain.Aggregates;

namespace OrderService.Domain.Repositories;

/// <summary>Write-side repository for the Order aggregate.</summary>
public interface IOrderRepository
{
    Task<Order?> GetByIdAsync(OrderId orderId, CancellationToken cancellationToken = default);
    Task SaveAsync(Order order, CancellationToken cancellationToken = default);
}
