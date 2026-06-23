using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.EventStore;
using ECommerceOrderProcessing.Infrastructure.Messaging;
using ECommerceOrderProcessing.Infrastructure.OutboxStore;
using ECommerceOrderProcessing.Infrastructure.Serialization;
using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Inventory;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using InventoryService.Domain.Aggregates;
using InventoryService.Domain.Events;
using InventoryService.Domain.Repositories;
using InventoryService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace InventoryService.Infrastructure.Repositories;

public sealed class EfCoreStockReservationRepository : IStockReservationRepository
{
    private readonly InventoryDbContext _db;
    private readonly IEventStore _eventStore;
    private readonly IOutboxStore _outboxStore;
    private readonly ILogger<EfCoreStockReservationRepository> _logger;

    public EfCoreStockReservationRepository(
        InventoryDbContext db,
        IEventStore eventStore,
        IOutboxStore outboxStore,
        ILogger<EfCoreStockReservationRepository> logger)
    {
        _db = db;
        _eventStore = eventStore;
        _outboxStore = outboxStore;
        _logger = logger;
    }

    public async Task<StockReservation?> GetByIdAsync(ReservationId reservationId, CancellationToken cancellationToken = default)
    {
        var events = await _eventStore.GetEventsAsync(reservationId.Value, cancellationToken);
        if (events.Count == 0)
            return null;

        return StockReservation.Rehydrate(events);
    }

    public async Task<IReadOnlyList<StockReservation>> GetExpiredReservationsAsync(CancellationToken cancellationToken = default)
    {
        var expiredIds = await _db.StockReservations
            .Where(r => r.Status == "Reserved" && r.ExpiresAt <= DateTimeOffset.UtcNow)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        var reservations = new List<StockReservation>(expiredIds.Count);
        foreach (var id in expiredIds)
        {
            var events = await _eventStore.GetEventsAsync(id, cancellationToken);
            if (events.Count > 0)
                reservations.Add(StockReservation.Rehydrate(events));
        }

        return reservations.AsReadOnly();
    }

    public async Task SaveAsync(
        StockReservation reservation,
        IReadOnlyList<Product> modifiedProducts,
        CancellationToken cancellationToken = default)
    {
        var uncommitted = reservation.UncommittedEvents;
        var expectedVersion = reservation.Version - uncommitted.Count;

        await _eventStore.AppendEventsAsync(
            reservation.Id, nameof(StockReservation), uncommitted, expectedVersion, cancellationToken);

        foreach (var evt in uncommitted)
        {
            var (eventType, payload, routingKey) = BuildOutboxEntry(evt, reservation);
            if (routingKey is not null)
            {
                await _outboxStore.AddAsync(
                    OutboxMessage.Create(eventType, payload, routingKey), cancellationToken);
            }
        }

        await UpsertReservationReadModelAsync(reservation, uncommitted, cancellationToken);
        await UpdateProductReadModelsAsync(modifiedProducts, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        reservation.ClearUncommittedEvents();

        _logger.LogDebug(
            "Saved reservation {ReservationId} with status {Status}",
            reservation.ReservationId, reservation.Status);
    }

    private (string eventType, string payload, string? routingKey) BuildOutboxEntry(
        DomainEvent evt,
        StockReservation reservation)
    {
        switch (evt)
        {
            case StockReservationCreated e:
            {
                var items = e.Items
                    .Select(i => new OrderItemData(i.ProductId, i.Quantity, Money.Create(0m, "USD")))
                    .ToList();
                var sharedEvent = new StockReserved(e.ReservationId, e.OrderId, items, e.Version, e.CorrelationId);
                return (
                    nameof(StockReserved),
                    JsonSerializer.Serialize(sharedEvent, typeof(StockReserved), InfrastructureJsonOptions.Default),
                    RoutingKeyBuilder.Build(sharedEvent));
            }

            case StockReservationFailed e:
            {
                var sharedEvent = new OutOfStock(
                    e.OrderId, e.FailedProductId,
                    e.RequestedQuantity, e.AvailableQuantity,
                    e.Version, e.CorrelationId);
                return (
                    nameof(OutOfStock),
                    JsonSerializer.Serialize(sharedEvent, typeof(OutOfStock), InfrastructureJsonOptions.Default),
                    RoutingKeyBuilder.Build(sharedEvent));
            }

            case StockReservationReleased e:
            {
                var sharedEvent = new StockReleased(e.ReservationId, e.OrderId, e.Version, e.CorrelationId);
                return (
                    nameof(StockReleased),
                    JsonSerializer.Serialize(sharedEvent, typeof(StockReleased), InfrastructureJsonOptions.Default),
                    RoutingKeyBuilder.Build(sharedEvent));
            }

            default:
                return (evt.GetType().Name, string.Empty, null);
        }
    }

    private async Task UpsertReservationReadModelAsync(
        StockReservation reservation,
        IReadOnlyList<DomainEvent> events,
        CancellationToken cancellationToken)
    {
        foreach (var evt in events)
        {
            switch (evt)
            {
                case StockReservationCreated e:
                    _db.StockReservations.Add(new StockReservationReadModel
                    {
                        Id = reservation.Id,
                        OrderId = reservation.OrderId.Value,
                        Status = "Reserved",
                        ItemsJson = JsonSerializer.Serialize(e.Items, InfrastructureJsonOptions.Default),
                        ExpiresAt = e.ExpiresAt,
                        CreatedAt = e.Timestamp
                    });
                    break;

                case StockReservationFailed e:
                    _db.StockReservations.Add(new StockReservationReadModel
                    {
                        Id = reservation.Id,
                        OrderId = reservation.OrderId.Value,
                        Status = "Failed",
                        ItemsJson = JsonSerializer.Serialize(e.Items, InfrastructureJsonOptions.Default),
                        ExpiresAt = DateTimeOffset.MinValue,
                        CreatedAt = e.Timestamp,
                        UpdatedAt = e.Timestamp
                    });
                    break;

                case StockReservationReleased:
                    await UpdateReservationStatusAsync(reservation.Id, "Released", cancellationToken);
                    break;

                case StockReservationExpired:
                    await UpdateReservationStatusAsync(reservation.Id, "Expired", cancellationToken);
                    break;
            }
        }
    }

    private async Task UpdateReservationStatusAsync(Guid id, string status, CancellationToken cancellationToken)
    {
        var existing = await _db.StockReservations.FindAsync(new object[] { id }, cancellationToken);
        if (existing is not null)
        {
            existing.Status = status;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task UpdateProductReadModelsAsync(
        IReadOnlyList<Product> modifiedProducts,
        CancellationToken cancellationToken)
    {
        if (modifiedProducts.Count == 0)
            return;

        var productIds = modifiedProducts.Select(p => p.ProductId.Value).ToList();
        var readModels = await _db.Products.Where(p => productIds.Contains(p.Id)).ToListAsync(cancellationToken);
        var readModelMap = readModels.ToDictionary(r => r.Id);

        var now = DateTimeOffset.UtcNow;
        foreach (var product in modifiedProducts)
        {
            if (readModelMap.TryGetValue(product.ProductId.Value, out var readModel))
            {
                readModel.AvailableQuantity = product.AvailableQuantity;
                readModel.ReservedQuantity = product.ReservedQuantity;
                readModel.UpdatedAt = now;
            }
        }
    }
}
