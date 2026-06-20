using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using AnalyticsService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AnalyticsService.Infrastructure.Repositories;

public class EfCoreCustomerMetricRepository : EfCoreAnalyticsRepository<CustomerMetric>, ICustomerMetricRepository
{
    public EfCoreCustomerMetricRepository(AnalyticsDbContext context) : base(context)
    {
    }

    public async Task<CustomerMetric?> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(c => c.CustomerId == customerId, cancellationToken);
    }

    public async Task<IEnumerable<CustomerMetric>> GetTopByLifetimeValueAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .OrderByDescending(c => c.LifetimeValue)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<CustomerMetric>> GetTopByOrderCountAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .OrderByDescending(c => c.OrderCount)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }
}
