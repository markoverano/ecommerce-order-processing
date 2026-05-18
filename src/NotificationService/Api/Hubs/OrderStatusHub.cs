using Microsoft.AspNetCore.SignalR;

namespace NotificationService.Api.Hubs;

/// <summary>SignalR hub for real-time order status updates. Clients join a group keyed on orderId.</summary>
public sealed class OrderStatusHub : Hub
{
    public Task JoinOrderGroup(string orderId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, $"order-{orderId}");

    public Task LeaveOrderGroup(string orderId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, $"order-{orderId}");
}
