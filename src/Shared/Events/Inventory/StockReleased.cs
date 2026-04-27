using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Events.Inventory;

/// <summary>Published when a reservation is released as part of saga compensation.</summary>
public sealed record StockReleased : DomainEvent
{
    public ReservationId ReservationId { get; init; }
    public OrderId OrderId { get; init; }

    public StockReleased(ReservationId reservationId, OrderId orderId, int version, Guid correlationId)
        : base(reservationId.Value, version, correlationId)
    {
        ReservationId = reservationId;
        OrderId = orderId;
    }
}
