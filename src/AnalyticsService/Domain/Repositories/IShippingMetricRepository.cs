using AnalyticsService.Domain.Entities;

namespace AnalyticsService.Domain.Repositories;

public interface IShippingMetricRepository : IAnalyticsRepository<ShippingMetric>
{
    Task<ShippingMetric?> GetByShipmentIdAsync(Guid shipmentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<ShippingMetric>> GetByCarrierAsync(string carrier, CancellationToken cancellationToken = default);
    Task<IEnumerable<ShippingMetric>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}
