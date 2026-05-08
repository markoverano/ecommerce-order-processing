using ECommerceOrderProcessing.Shared.ValueObjects;
using InventoryService.Application.DTOs;

namespace InventoryService.Application.Repositories;

/// <summary>Read-side repository for denormalized product inventory view models.</summary>
public interface IStockReadRepository
{
    Task<StockDto?> GetByProductIdAsync(ProductId productId, CancellationToken cancellationToken = default);
}
