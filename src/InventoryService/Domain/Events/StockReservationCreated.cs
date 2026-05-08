using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace InventoryService.Domain.Events;

internal sealed record StockReservationCreated : DomainEvent
{
    public ReservationId ReservationId { get; init; }
    public OrderId OrderId { get; init; }
    public IReadOnlyList<StockReservationItem> Items { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }

    public StockReservationCreated(
        ReservationId reservationId,
        OrderId orderId,
        IReadOnlyList<StockReservationItem> items,
        DateTimeOffset expiresAt,
        int version,
        Guid correlationId)
        : base(reservationId.Value, version, correlationId)
    {
        ReservationId = reservationId;
        OrderId = orderId;
        Items = items;
        ExpiresAt = expiresAt;
    }
}
