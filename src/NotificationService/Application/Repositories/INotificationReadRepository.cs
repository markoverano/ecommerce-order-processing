using ECommerceOrderProcessing.Shared.ValueObjects;
using NotificationService.Application.DTOs;

namespace NotificationService.Application.Repositories;

/// <summary>Read-side repository for denormalized notification view models.</summary>
public interface INotificationReadRepository
{
    Task<NotificationDto?> GetByIdAsync(NotificationId notificationId, CancellationToken cancellationToken = default);
    Task<NotificationId?> FindByProviderMessageIdAsync(string providerMessageId, CancellationToken cancellationToken = default);
}
