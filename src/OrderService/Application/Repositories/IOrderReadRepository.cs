using ECommerceOrderProcessing.Shared.ValueObjects;
using OrderService.Application.DTOs;

namespace OrderService.Application.Repositories;

/// <summary>Read-side repository serving denormalized order view models.</summary>
public interface IOrderReadRepository
{
    Task<OrderDto?> GetByIdAsync(OrderId orderId, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderDto>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default);
}
