using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using AnalyticsService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AnalyticsService.Infrastructure.Repositories;

public class EfCoreOrderMetricRepository : EfCoreAnalyticsRepository<OrderMetric>, IOrderMetricRepository
{
    public EfCoreOrderMetricRepository(AnalyticsDbContext context) : base(context)
    {
    }

    public async Task<OrderMetric?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(o => o.OrderId == orderId, cancellationToken);
    }

    public async Task<IEnumerable<OrderMetric>> GetByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(o => o.CustomerId == customerId)
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<OrderMetric>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(o => o.CreatedAt >= startDate && o.CreatedAt <= endDate)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
