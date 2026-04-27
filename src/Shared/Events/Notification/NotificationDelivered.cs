using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Notification;

/// <summary>Published when Mailgun or Twilio delivery webhook confirms receipt by the recipient.</summary>
public sealed record NotificationDelivered : DomainEvent
{
    public NotificationId NotificationId { get; init; }
    public DateTimeOffset DeliveredAt { get; init; }

    public NotificationDelivered(NotificationId notificationId, DateTimeOffset deliveredAt, int version, Guid correlationId)
        : base(notificationId.Value, version, correlationId)
    {
        NotificationId = notificationId;
        DeliveredAt = deliveredAt;
    }
}
