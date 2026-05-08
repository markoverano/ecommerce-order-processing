using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;
using InventoryService.Domain.Enums;
using InventoryService.Domain.Events;
using InventoryService.Domain.Exceptions;

namespace InventoryService.Domain.Aggregates;

public sealed class StockReservation : AggregateRoot
{
    public ReservationId ReservationId => ReservationId.From(Id);
    public OrderId OrderId { get; private set; }
    public IReadOnlyList<StockReservationItem> Items { get; private set; } = Array.Empty<StockReservationItem>();
    public ReservationStatus Status { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    private StockReservation() { }

    public static StockReservation Create(
        OrderId orderId,
        IReadOnlyList<StockReservationItem> items,
        Guid correlationId)
    {
        var reservationId = ReservationId.New();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(2);
        var reservation = new StockReservation();
        reservation.RaiseEvent(new StockReservationCreated(reservationId, orderId, items, expiresAt, 1, correlationId));
        return reservation;
    }

    // Records a failed reservation attempt for audit; causes OutOfStock to be published via outbox.
    public static StockReservation Fail(
        OrderId orderId,
        IReadOnlyList<StockReservationItem> items,
        ProductId failedProductId,
        int requestedQuantity,
        int availableQuantity,
        Guid correlationId)
    {
        var reservationId = ReservationId.New();
        var reservation = new StockReservation();
        reservation.RaiseEvent(new StockReservationFailed(
            reservationId, orderId, items, failedProductId,
            requestedQuantity, availableQuantity, 1, correlationId));
        return reservation;
    }

    public void Release(Guid correlationId)
    {
        if (Status != ReservationStatus.Reserved)
            throw new StockReservationException($"Cannot release a reservation with status {Status}.");
        RaiseEvent(new StockReservationReleased(ReservationId, OrderId, Items, Version + 1, correlationId));
    }

    public void Expire(Guid correlationId)
    {
        if (Status != ReservationStatus.Reserved)
            throw new StockReservationException($"Cannot expire a reservation with status {Status}.");
        RaiseEvent(new StockReservationExpired(ReservationId, OrderId, Items, Version + 1, correlationId));
    }

    // Reconstructs aggregate state from a persisted event stream without raising new uncommitted events.
    public static StockReservation Rehydrate(IReadOnlyList<DomainEvent> events)
    {
        if (events.Count == 0)
            throw new InvalidOperationException("Cannot rehydrate a StockReservation from an empty event stream.");

        var reservation = new StockReservation();
        foreach (var evt in events)
        {
            reservation.Apply(evt);
            reservation.Version++;
        }
        return reservation;
    }

    protected override void Apply(DomainEvent domainEvent)
    {
        switch (domainEvent)
        {
            case StockReservationCreated e:
                Id = e.AggregateId;
                OrderId = e.OrderId;
                Items = e.Items;
                ExpiresAt = e.ExpiresAt;
                Status = ReservationStatus.Reserved;
                break;
            case StockReservationFailed e:
                Id = e.AggregateId;
                OrderId = e.OrderId;
                Items = e.Items;
                Status = ReservationStatus.Failed;
                break;
            case StockReservationReleased:
                Status = ReservationStatus.Released;
                break;
            case StockReservationExpired:
                Status = ReservationStatus.Expired;
                break;
        }
    }
}
