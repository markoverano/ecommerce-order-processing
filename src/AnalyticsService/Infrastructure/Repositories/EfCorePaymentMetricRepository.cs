using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using AnalyticsService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AnalyticsService.Infrastructure.Repositories;

public class EfCorePaymentMetricRepository : EfCoreAnalyticsRepository<PaymentMetric>, IPaymentMetricRepository
{
    public EfCorePaymentMetricRepository(AnalyticsDbContext context) : base(context)
    {
    }

    public async Task<PaymentMetric?> GetByPaymentIdAsync(Guid paymentId, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(p => p.PaymentId == paymentId, cancellationToken);
    }

    public async Task<IEnumerable<PaymentMetric>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(p => p.Status == status)
            .OrderByDescending(p => p.ProcessedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<PaymentMetric>> GetByDateRangeAsync(DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(p => p.ProcessedAt >= startDate && p.ProcessedAt <= endDate)
            .OrderBy(p => p.ProcessedAt)
            .ToListAsync(cancellationToken);
    }
}
