using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.EventStore;
using ECommerceOrderProcessing.Infrastructure.Messaging;
using ECommerceOrderProcessing.Infrastructure.OutboxStore;
using ECommerceOrderProcessing.Infrastructure.Serialization;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Shipping;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Logging;
using ShippingService.Domain.Aggregates;
using ShippingService.Domain.Events;
using ShippingService.Domain.Exceptions;
using ShippingService.Domain.Repositories;
using ShippingService.Infrastructure.Persistence;

namespace ShippingService.Infrastructure.Repositories;

public sealed class EfCoreShipmentRepository : IShipmentRepository
{
    private readonly ShippingDbContext _db;
    private readonly IEventStore _eventStore;
    private readonly IOutboxStore _outboxStore;
    private readonly ILogger<EfCoreShipmentRepository> _logger;

    public EfCoreShipmentRepository(
        ShippingDbContext db,
        IEventStore eventStore,
        IOutboxStore outboxStore,
        ILogger<EfCoreShipmentRepository> logger)
    {
        _db = db;
        _eventStore = eventStore;
        _outboxStore = outboxStore;
        _logger = logger;
    }

    public async Task<Shipment?> GetByIdAsync(ShipmentId shipmentId, CancellationToken cancellationToken = default)
    {
        var events = await _eventStore.GetEventsAsync(shipmentId.Value, cancellationToken);
        if (events.Count == 0)
            return null;

        return Shipment.Rehydrate(events);
    }

    public async Task SaveAsync(Shipment shipment, CancellationToken cancellationToken = default)
    {
        var uncommitted = shipment.UncommittedEvents;
        var expectedVersion = shipment.Version - uncommitted.Count;

        await _eventStore.AppendEventsAsync(shipment.Id, nameof(Shipment), uncommitted, expectedVersion, cancellationToken);

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

        await UpdateViewModelAsync(shipment, uncommitted, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        shipment.ClearUncommittedEvents();

        _logger.LogDebug("Saved shipment {ShipmentId}, version {Version}", shipment.ShipmentId, shipment.Version);
    }

    private static string? GetRoutingKey(DomainEvent evt) => evt switch
    {
        ShipmentCreated or ShipmentFailed or ShipmentDispatched or DeliveryConfirmed or ShipmentCancelled => RoutingKeyBuilder.Build(evt),
        _ => null
    };

    private async Task UpdateViewModelAsync(Shipment shipment, IReadOnlyList<DomainEvent> events, CancellationToken cancellationToken)
    {
        foreach (var evt in events)
        {
            switch (evt)
            {
                case ShipmentBooked booked:
                    InsertViewModel(shipment, booked);
                    break;
                case ShipmentCreated created:
                    await UpdateViewModelAsync(shipment, created.TrackingNumber, cancellationToken);
                    break;
                case ShipmentFailed:
                case ShipmentDispatched:
                case DeliveryConfirmed:
                case ShipmentCancelled:
                    await UpdateViewModelStatusAsync(shipment, cancellationToken);
                    break;
            }
        }
    }

    private void InsertViewModel(Shipment shipment, ShipmentBooked booked)
    {
        var addr = shipment.Destination;
        var addressLine = $"{addr.Line1}, {addr.City}, {addr.PostalCode}, {addr.CountryCode}";

        _db.ShipmentViewModels.Add(new ShipmentReadModel
        {
            Id = shipment.Id,
            OrderId = shipment.OrderId.Value,
            CustomerId = shipment.CustomerId.Value,
            Status = shipment.Status.ToString(),
            TrackingNumber = null,
            DestinationAddress = addressLine,
            CreatedAt = booked.Timestamp,
            UpdatedAt = null
        });
    }

    private async Task UpdateViewModelAsync(Shipment shipment, string trackingNumber, CancellationToken cancellationToken)
    {
        var existing = await _db.ShipmentViewModels.FindAsync(new object[] { shipment.Id }, cancellationToken)
            ?? throw new ShipmentNotFoundException(shipment.Id);

        existing.Status = shipment.Status.ToString();
        existing.TrackingNumber = trackingNumber;
        existing.UpdatedAt = DateTimeOffset.UtcNow;
    }

    private async Task UpdateViewModelStatusAsync(Shipment shipment, CancellationToken cancellationToken)
    {
        var existing = await _db.ShipmentViewModels.FindAsync(new object[] { shipment.Id }, cancellationToken)
            ?? throw new ShipmentNotFoundException(shipment.Id);

        existing.Status = shipment.Status.ToString();
        existing.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
