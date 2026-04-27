using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Inventory;

/// <summary>Published when Inventory Service successfully holds stock for an order.</summary>
public sealed record StockReserved : DomainEvent
{
    public ReservationId ReservationId { get; init; }
    public OrderId OrderId { get; init; }
    public IReadOnlyList<OrderItemData> Items { get; init; }

    public StockReserved(ReservationId reservationId, OrderId orderId, IReadOnlyList<OrderItemData> items, int version, Guid correlationId)
        : base(reservationId.Value, version, correlationId)
    {
        ReservationId = reservationId;
        OrderId = orderId;
        Items = items;
    }
}
