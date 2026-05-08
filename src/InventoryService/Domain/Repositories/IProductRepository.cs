using ECommerceOrderProcessing.Shared.ValueObjects;
using InventoryService.Domain.Aggregates;

namespace InventoryService.Domain.Repositories;

/// <summary>Read-side access to product aggregates. Products are persisted via IStockReservationRepository.SaveAsync.</summary>
public interface IProductRepository
{
    Task<Product?> GetByIdAsync(ProductId productId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Product>> GetByIdsAsync(IReadOnlyList<ProductId> productIds, CancellationToken cancellationToken = default);
}
