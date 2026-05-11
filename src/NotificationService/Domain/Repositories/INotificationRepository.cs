using ECommerceOrderProcessing.Shared.ValueObjects;
using NotificationService.Domain.Aggregates;

namespace NotificationService.Domain.Repositories;

/// <summary>Write-side repository for the Notification aggregate.</summary>
public interface INotificationRepository
{
    Task<Notification?> GetByIdAsync(NotificationId notificationId, CancellationToken cancellationToken = default);
    Task SaveAsync(Notification notification, CancellationToken cancellationToken = default);
}
