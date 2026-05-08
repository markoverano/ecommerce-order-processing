using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace InventoryService.Domain.Events;

internal sealed record StockReservationReleased : DomainEvent
{
    public ReservationId ReservationId { get; init; }
    public OrderId OrderId { get; init; }
    public IReadOnlyList<StockReservationItem> Items { get; init; }

    public StockReservationReleased(
        ReservationId reservationId,
        OrderId orderId,
        IReadOnlyList<StockReservationItem> items,
        int version,
        Guid correlationId)
        : base(reservationId.Value, version, correlationId)
    {
        ReservationId = reservationId;
        OrderId = orderId;
        Items = items;
    }
}
