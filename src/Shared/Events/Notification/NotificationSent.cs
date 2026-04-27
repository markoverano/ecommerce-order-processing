using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Notification;

/// <summary>Published when Mailgun or Twilio accepts the message for delivery.</summary>
public sealed record NotificationSent : DomainEvent
{
    public NotificationId NotificationId { get; init; }
    public OrderId OrderId { get; init; }
    public CustomerId CustomerId { get; init; }
    public string NotificationType { get; init; }

    public NotificationSent(NotificationId notificationId, OrderId orderId, CustomerId customerId, string notificationType, int version, Guid correlationId)
        : base(notificationId.Value, version, correlationId)
    {
        NotificationId = notificationId;
        OrderId = orderId;
        CustomerId = customerId;
        NotificationType = notificationType;
    }
}
