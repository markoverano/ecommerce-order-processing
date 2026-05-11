using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Notification;
using ECommerceOrderProcessing.Shared.ValueObjects;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Events;
using NotificationService.Domain.Exceptions;

namespace NotificationService.Domain.Aggregates;

public sealed class Notification : AggregateRoot
{
    public NotificationId NotificationId => NotificationId.From(Id);
    public OrderId OrderId { get; private set; }
    public CustomerId CustomerId { get; private set; }
    public string NotificationType { get; private set; } = string.Empty;
    public NotificationStatus Status { get; private set; }
    public string Channel { get; private set; } = string.Empty;
    public string RecipientAddress { get; private set; } = string.Empty;
    public string? ProviderMessageId { get; private set; }

    private Notification() { }

    public static Notification Create(
        OrderId orderId,
        CustomerId customerId,
        string notificationType,
        string channel,
        string recipientAddress,
        IReadOnlyDictionary<string, string> templateData,
        Guid correlationId)
    {
        ArgumentNullException.ThrowIfNull(templateData);
        if (string.IsNullOrWhiteSpace(channel))
            throw new NotificationException("Channel is required.");
        if (string.IsNullOrWhiteSpace(recipientAddress))
            throw new NotificationException("Recipient address is required.");

        var notificationId = NotificationId.New();
        var notification = new Notification();
        notification.RaiseEvent(new NotificationQueued(
            notificationId, orderId, customerId, notificationType, channel, recipientAddress, templateData, 1, correlationId));
        return notification;
    }

    public void MarkAsSent(string? providerMessageId, Guid correlationId)
    {
        if (Status != NotificationStatus.Pending)
            throw new NotificationException($"Cannot mark a {Status} notification as sent.");
        RaiseEvent(new NotificationSent(NotificationId, OrderId, CustomerId, NotificationType, providerMessageId, Version + 1, correlationId));
    }

    public void MarkAsDelivered(DateTimeOffset deliveredAt, Guid correlationId)
    {
        if (Status != NotificationStatus.Sent)
            throw new NotificationException($"Cannot mark a {Status} notification as delivered.");
        RaiseEvent(new NotificationDelivered(NotificationId, deliveredAt, Version + 1, correlationId));
    }

    public void MarkAsFailed(string reason, Guid correlationId)
    {
        if (Status == NotificationStatus.Delivered)
            throw new NotificationException("Cannot fail a delivered notification.");
        RaiseEvent(new NotificationFailed(NotificationId, reason, Version + 1, correlationId));
    }

    // Reconstructs aggregate state from a persisted event stream without raising new uncommitted events.
    public static Notification Rehydrate(IReadOnlyList<DomainEvent> events)
    {
        if (events.Count == 0)
            throw new InvalidOperationException("Cannot rehydrate a Notification from an empty event stream.");

        var notification = new Notification();
        foreach (var evt in events)
        {
            notification.Apply(evt);
            notification.Version++;
        }
        return notification;
    }

    protected override void Apply(DomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case NotificationQueued e:
                Id = e.AggregateId;
                OrderId = e.OrderId;
                CustomerId = e.CustomerId;
                NotificationType = e.NotificationType;
                Channel = e.Channel;
                RecipientAddress = e.RecipientAddress;
                Status = NotificationStatus.Pending;
                break;
            case NotificationSent e:
                Status = NotificationStatus.Sent;
                ProviderMessageId = e.ProviderMessageId;
                break;
            case NotificationDelivered:
                Status = NotificationStatus.Delivered;
                break;
            case NotificationFailed:
                Status = NotificationStatus.Failed;
                break;
        }
    }
}
