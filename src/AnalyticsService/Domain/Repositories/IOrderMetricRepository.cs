using AnalyticsService.Domain.Entities;

namespace AnalyticsService.Domain.Repositories;

public interface IOrderMetricRepository : IAnalyticsRepository<OrderMetric>
{
    Task<OrderMetric?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrderMetric>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<OrderMetric>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default);
}
