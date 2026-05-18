using ECommerceOrderProcessing.Shared.SignalR;

namespace NotificationService.Application.Notifications;

/// <summary>Pushes saga step updates to connected browser clients. No SignalR types cross this boundary.</summary>
public interface IOrderStatusNotifier
{
    Task NotifyAsync(OrderStatusUpdate update, CancellationToken cancellationToken = default);
}
