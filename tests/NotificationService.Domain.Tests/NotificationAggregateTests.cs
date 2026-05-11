using ECommerceOrderProcessing.Shared.Events.Notification;
using ECommerceOrderProcessing.Shared.ValueObjects;
using NotificationService.Domain.Aggregates;
using NotificationService.Domain.Enums;
using NotificationService.Domain.Events;
using NotificationService.Domain.Exceptions;
using Xunit;

namespace NotificationService.Domain.Tests;

public sealed class NotificationAggregateTests
{
    private static readonly OrderId SomeOrder = new(Guid.NewGuid());
    private static readonly CustomerId SomeCustomer = new(Guid.NewGuid());
    private const string SomeType = "OrderConfirmed";
    private static readonly IReadOnlyDictionary<string, string> SomeTemplate =
        new Dictionary<string, string> { ["orderId"] = "ORD-001" };

    [Fact]
    public void Create_WithEmail_SetsStatusPending()
    {
        var notification = Notification.Create(SomeOrder, SomeCustomer, SomeType, "email", "user@example.com", SomeTemplate, Guid.NewGuid());

        Assert.Equal(NotificationStatus.Pending, notification.Status);
    }

    [Fact]
    public void Create_WithValidData_RaisesNotificationQueuedEvent()
    {
        var notification = Notification.Create(SomeOrder, SomeCustomer, SomeType, "email", "user@example.com", SomeTemplate, Guid.NewGuid());

        Assert.Single(notification.UncommittedEvents);
        Assert.IsType<NotificationQueued>(notification.UncommittedEvents[0]);
    }

    [Fact]
    public void Create_AssignsNonEmptyId()
    {
        var notification = Notification.Create(SomeOrder, SomeCustomer, SomeType, "email", "user@example.com", SomeTemplate, Guid.NewGuid());

        Assert.NotEqual(Guid.Empty, notification.Id);
    }

    [Fact]
    public void Create_WithEmptyChannel_ThrowsNotificationException()
    {
        Assert.Throws<NotificationException>(() =>
            Notification.Create(SomeOrder, SomeCustomer, SomeType, string.Empty, "user@example.com", SomeTemplate, Guid.NewGuid()));
    }

    [Fact]
    public void Create_WithEmptyRecipient_ThrowsNotificationException()
    {
        Assert.Throws<NotificationException>(() =>
            Notification.Create(SomeOrder, SomeCustomer, SomeType, "email", string.Empty, SomeTemplate, Guid.NewGuid()));
    }

    [Fact]
    public void MarkAsSent_FromPendingState_SetsStatusSent()
    {
        var notification = CreatePendingNotification();

        notification.MarkAsSent("msg-id-123", Guid.NewGuid());

        Assert.Equal(NotificationStatus.Sent, notification.Status);
    }

    [Fact]
    public void MarkAsSent_FromPendingState_RaisesNotificationSentEvent()
    {
        var notification = CreatePendingNotification();

        notification.MarkAsSent("msg-id-123", Guid.NewGuid());

        Assert.Contains(notification.UncommittedEvents, e => e is NotificationSent);
    }

    [Fact]
    public void MarkAsSent_StoresProviderMessageId()
    {
        var notification = CreatePendingNotification();

        notification.MarkAsSent("msg-id-abc", Guid.NewGuid());

        Assert.Equal("msg-id-abc", notification.ProviderMessageId);
    }

    [Fact]
    public void MarkAsSent_FromSentState_ThrowsNotificationException()
    {
        var notification = CreateSentNotification();

        Assert.Throws<NotificationException>(() =>
            notification.MarkAsSent("msg-id-second", Guid.NewGuid()));
    }

    [Fact]
    public void MarkAsDelivered_FromSentState_SetsStatusDelivered()
    {
        var notification = CreateSentNotification();

        notification.MarkAsDelivered(DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Equal(NotificationStatus.Delivered, notification.Status);
    }

    [Fact]
    public void MarkAsDelivered_FromSentState_RaisesNotificationDeliveredEvent()
    {
        var notification = CreateSentNotification();

        notification.MarkAsDelivered(DateTimeOffset.UtcNow, Guid.NewGuid());

        Assert.Contains(notification.UncommittedEvents, e => e is NotificationDelivered);
    }

    [Fact]
    public void MarkAsDelivered_FromPendingState_ThrowsNotificationException()
    {
        var notification = CreatePendingNotification();

        Assert.Throws<NotificationException>(() =>
            notification.MarkAsDelivered(DateTimeOffset.UtcNow, Guid.NewGuid()));
    }

    [Fact]
    public void MarkAsFailed_FromPendingState_SetsStatusFailed()
    {
        var notification = CreatePendingNotification();

        notification.MarkAsFailed("Provider rejected.", Guid.NewGuid());

        Assert.Equal(NotificationStatus.Failed, notification.Status);
    }

    [Fact]
    public void MarkAsFailed_FromPendingState_RaisesNotificationFailedEvent()
    {
        var notification = CreatePendingNotification();

        notification.MarkAsFailed("Provider rejected.", Guid.NewGuid());

        Assert.Contains(notification.UncommittedEvents, e => e is NotificationFailed);
    }

    [Fact]
    public void MarkAsFailed_FromDeliveredState_ThrowsNotificationException()
    {
        var notification = CreateSentNotification();
        notification.MarkAsDelivered(DateTimeOffset.UtcNow, Guid.NewGuid());
        notification.ClearUncommittedEvents();

        Assert.Throws<NotificationException>(() =>
            notification.MarkAsFailed("reason", Guid.NewGuid()));
    }

    [Fact]
    public void Rehydrate_FromEvents_ReconstructsStateCorrectly()
    {
        var original = Notification.Create(SomeOrder, SomeCustomer, SomeType, "email", "user@example.com", SomeTemplate, Guid.NewGuid());
        original.MarkAsSent("msg-id-rehydrate", Guid.NewGuid());
        var events = original.UncommittedEvents;

        var rehydrated = Notification.Rehydrate(events);

        Assert.Equal(original.Id, rehydrated.Id);
        Assert.Equal(NotificationStatus.Sent, rehydrated.Status);
        Assert.Equal("msg-id-rehydrate", rehydrated.ProviderMessageId);
    }

    [Fact]
    public void Rehydrate_FromEmptyEventList_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            Notification.Rehydrate(Array.Empty<ECommerceOrderProcessing.Shared.Domain.DomainEvent>()));
    }

    [Fact]
    public void ClearUncommittedEvents_RemovesAllEvents()
    {
        var notification = Notification.Create(SomeOrder, SomeCustomer, SomeType, "email", "user@example.com", SomeTemplate, Guid.NewGuid());

        notification.ClearUncommittedEvents();

        Assert.Empty(notification.UncommittedEvents);
    }

    [Fact]
    public void Create_WithSmsChannel_SetsChannelCorrectly()
    {
        var notification = Notification.Create(SomeOrder, SomeCustomer, SomeType, "sms", "+15551234567", SomeTemplate, Guid.NewGuid());

        Assert.Equal("sms", notification.Channel);
        Assert.Equal("+15551234567", notification.RecipientAddress);
    }

    private static Notification CreatePendingNotification()
    {
        var notification = Notification.Create(SomeOrder, SomeCustomer, SomeType, "email", "user@example.com", SomeTemplate, Guid.NewGuid());
        notification.ClearUncommittedEvents();
        return notification;
    }

    private static Notification CreateSentNotification()
    {
        var notification = CreatePendingNotification();
        notification.MarkAsSent("msg-123", Guid.NewGuid());
        notification.ClearUncommittedEvents();
        return notification;
    }
}
