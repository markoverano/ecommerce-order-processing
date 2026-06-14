using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.EventStore;
using ECommerceOrderProcessing.Infrastructure.Messaging;
using ECommerceOrderProcessing.Infrastructure.OutboxStore;
using ECommerceOrderProcessing.Infrastructure.Serialization;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Notification;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Logging;
using NotificationService.Domain.Aggregates;
using NotificationService.Domain.Events;
using NotificationService.Domain.Exceptions;
using NotificationService.Domain.Repositories;
using NotificationService.Infrastructure.Persistence;

namespace NotificationService.Infrastructure.Repositories;

public sealed class EfCoreNotificationRepository : INotificationRepository
{
    private readonly NotificationDbContext _db;
    private readonly IEventStore _eventStore;
    private readonly IOutboxStore _outboxStore;
    private readonly ILogger<EfCoreNotificationRepository> _logger;

    public EfCoreNotificationRepository(
        NotificationDbContext db,
        IEventStore eventStore,
        IOutboxStore outboxStore,
        ILogger<EfCoreNotificationRepository> logger)
    {
        _db = db;
        _eventStore = eventStore;
        _outboxStore = outboxStore;
        _logger = logger;
    }

    public async Task<Notification?> GetByIdAsync(NotificationId notificationId, CancellationToken cancellationToken = default)
    {
        var events = await _eventStore.GetEventsAsync(notificationId.Value, cancellationToken);
        if (events.Count == 0)
            return null;

        return Notification.Rehydrate(events);
    }

    public async Task SaveAsync(Notification notification, CancellationToken cancellationToken = default)
    {
        var uncommitted = notification.UncommittedEvents;
        var expectedVersion = notification.Version - uncommitted.Count;

        await _eventStore.AppendEventsAsync(notification.Id, nameof(Notification), uncommitted, expectedVersion, cancellationToken);

        foreach (var evt in uncommitted)
        {
            var routingKey = GetRoutingKey(evt);
            if (routingKey is not null)
            {
                var outboxMessage = OutboxMessage.Create(
                    evt.GetType().Name,
                    JsonSerializer.Serialize(evt, evt.GetType(), InfrastructureJsonOptions.Default),
                    routingKey);
                await _outboxStore.AddAsync(outboxMessage, cancellationToken);
            }
        }

        await UpdateViewModelAsync(notification, uncommitted, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        notification.ClearUncommittedEvents();

        _logger.LogDebug("Saved notification {NotificationId}, version {Version}", notification.NotificationId, notification.Version);
    }

    private static string? GetRoutingKey(DomainEvent evt) => evt switch
    {
        NotificationSent or NotificationFailed or NotificationDelivered => RoutingKeyBuilder.Build(evt),
        _ => null
    };

    private async Task UpdateViewModelAsync(
        Notification notification,
        IReadOnlyList<DomainEvent> events,
        CancellationToken cancellationToken)
    {
        foreach (var evt in events)
        {
            switch (evt)
            {
                case NotificationQueued created:
                    InsertViewModel(notification, created);
                    break;
                case NotificationSent sent:
                    await UpdateViewModelSentAsync(notification, sent.ProviderMessageId, cancellationToken);
                    break;
                case NotificationDelivered:
                case NotificationFailed:
                    await UpdateViewModelStatusAsync(notification, cancellationToken);
                    break;
            }
        }
    }

    private void InsertViewModel(Notification notification, NotificationQueued created)
    {
        _db.NotificationViewModels.Add(new NotificationReadModel
        {
            Id = notification.Id,
            OrderId = notification.OrderId.Value,
            CustomerId = notification.CustomerId.Value,
            NotificationType = notification.NotificationType,
            Status = notification.Status.ToString(),
            Channel = notification.Channel,
            RecipientAddress = notification.RecipientAddress,
            ProviderMessageId = null,
            CreatedAt = created.Timestamp,
            UpdatedAt = null
        });
    }

    private async Task UpdateViewModelSentAsync(Notification notification, string? providerMessageId, CancellationToken cancellationToken)
    {
        var existing = await _db.NotificationViewModels.FindAsync(new object[] { notification.Id }, cancellationToken)
            ?? throw new NotificationNotFoundException(notification.Id);

        existing.Status = notification.Status.ToString();
        existing.ProviderMessageId = providerMessageId;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task UpdateViewModelStatusAsync(Notification notification, CancellationToken cancellationToken)
    {
        var existing = await _db.NotificationViewModels.FindAsync(new object[] { notification.Id }, cancellationToken)
            ?? throw new NotificationNotFoundException(notification.Id);

        existing.Status = notification.Status.ToString();
        existing.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
