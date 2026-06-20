using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using AnalyticsService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AnalyticsService.Infrastructure.Repositories;

public class EfCoreNotificationMetricRepository : EfCoreAnalyticsRepository<NotificationMetric>, INotificationMetricRepository
{
    public EfCoreNotificationMetricRepository(AnalyticsDbContext context) : base(context)
    {
    }

    public async Task<NotificationMetric?> GetByNotificationIdAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        return await DbSet.FirstOrDefaultAsync(n => n.NotificationId == notificationId, cancellationToken);
    }

    public async Task<IEnumerable<NotificationMetric>> GetByTypeAsync(string type, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(n => n.Type == type)
            .OrderByDescending(n => n.SentAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<NotificationMetric>> GetByStatusAsync(string status, CancellationToken cancellationToken = default)
    {
        return await DbSet
            .Where(n => n.Status == status)
            .OrderByDescending(n => n.SentAt)
            .ToListAsync(cancellationToken);
    }
}
