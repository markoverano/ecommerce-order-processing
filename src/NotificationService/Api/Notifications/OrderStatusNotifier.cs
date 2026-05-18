using ECommerceOrderProcessing.Shared.SignalR;
using Microsoft.AspNetCore.SignalR;
using NotificationService.Api.Hubs;
using NotificationService.Application.Notifications;

namespace NotificationService.Api.Notifications;

public sealed class OrderStatusNotifier : IOrderStatusNotifier
{
    private readonly IHubContext<OrderStatusHub> _hubContext;

    public OrderStatusNotifier(IHubContext<OrderStatusHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyAsync(OrderStatusUpdate update, CancellationToken cancellationToken = default) =>
        _hubContext.Clients
            .Group($"order-{update.OrderId}")
            .SendCoreAsync("ReceiveOrderUpdate", new object[] { update }, cancellationToken);
}
