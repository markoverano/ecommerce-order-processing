using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Notification;

/// <summary>Published when the notification provider permanently rejects the message.</summary>
public sealed record NotificationFailed : DomainEvent
{
    public NotificationId NotificationId { get; init; }
    public string Reason { get; init; }

    public NotificationFailed(NotificationId notificationId, string reason, int version, Guid correlationId)
        : base(notificationId.Value, version, correlationId)
    {
        NotificationId = notificationId;
        Reason = reason;
    }
}
