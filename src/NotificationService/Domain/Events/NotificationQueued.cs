using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace NotificationService.Domain.Events;

public sealed record NotificationQueued : DomainEvent
{
    public NotificationId NotificationId { get; init; }
    public OrderId OrderId { get; init; }
    public CustomerId CustomerId { get; init; }
    public string NotificationType { get; init; }
    public string Channel { get; init; }
    public string RecipientAddress { get; init; }
    public IReadOnlyDictionary<string, string> TemplateData { get; init; }

    public NotificationQueued(
        NotificationId notificationId,
        OrderId orderId,
        CustomerId customerId,
        string notificationType,
        string channel,
        string recipientAddress,
        IReadOnlyDictionary<string, string> templateData,
        int version,
        Guid correlationId)
        : base(notificationId.Value, version, correlationId)
    {
        NotificationId = notificationId;
        OrderId = orderId;
        CustomerId = customerId;
        NotificationType = notificationType;
        Channel = channel;
        RecipientAddress = recipientAddress;
        TemplateData = templateData;
    }
}
