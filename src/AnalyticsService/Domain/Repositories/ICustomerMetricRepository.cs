using AnalyticsService.Domain.Entities;

namespace AnalyticsService.Domain.Repositories;

public interface ICustomerMetricRepository : IAnalyticsRepository<CustomerMetric>
{
    Task<CustomerMetric?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
    Task<IEnumerable<CustomerMetric>> GetTopByLifetimeValueAsync(int limit, CancellationToken cancellationToken = default);
    Task<IEnumerable<CustomerMetric>> GetTopByOrderCountAsync(int limit, CancellationToken cancellationToken = default);
}
