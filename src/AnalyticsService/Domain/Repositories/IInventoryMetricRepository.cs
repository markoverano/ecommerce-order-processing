using AnalyticsService.Domain.Entities;

namespace AnalyticsService.Domain.Repositories;

public interface IInventoryMetricRepository : IAnalyticsRepository<InventoryMetric>
{
    Task<InventoryMetric?> GetByReservationIdAsync(Guid reservationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<InventoryMetric>> GetByProductIdAsync(Guid productId, CancellationToken cancellationToken = default);
}
