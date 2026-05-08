using ECommerceOrderProcessing.Shared.ValueObjects;
using InventoryService.Domain.Aggregates;

namespace InventoryService.Domain.Repositories;

/// <summary>
/// Write-side repository for StockReservation aggregates.
/// SaveAsync persists reservation events, updates product read models, and writes outbox entries atomically.
/// </summary>
public interface IStockReservationRepository
{
    Task<StockReservation?> GetByIdAsync(ReservationId reservationId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StockReservation>> GetExpiredReservationsAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(StockReservation reservation, IReadOnlyList<Product> modifiedProducts, CancellationToken cancellationToken = default);
}
