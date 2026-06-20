using AnalyticsService.Domain.Entities;

namespace AnalyticsService.Domain.Repositories;

public interface INotificationMetricRepository : IAnalyticsRepository<NotificationMetric>
{
    Task<NotificationMetric?> GetByNotificationIdAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task<IEnumerable<NotificationMetric>> GetByTypeAsync(string type, CancellationToken cancellationToken = default);
    Task<IEnumerable<NotificationMetric>> GetByStatusAsync(string status, CancellationToken cancellationToken = default);
}
