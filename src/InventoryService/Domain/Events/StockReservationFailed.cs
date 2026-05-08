using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;

namespace InventoryService.Domain.Events;

internal sealed record StockReservationFailed : DomainEvent
{
    public ReservationId ReservationId { get; init; }
    public OrderId OrderId { get; init; }
    public IReadOnlyList<StockReservationItem> Items { get; init; }
    public ProductId FailedProductId { get; init; }
    public int RequestedQuantity { get; init; }
    public int AvailableQuantity { get; init; }

    public StockReservationFailed(
        ReservationId reservationId,
        OrderId orderId,
        IReadOnlyList<StockReservationItem> items,
        ProductId failedProductId,
        int requestedQuantity,
        int availableQuantity,
        int version,
        Guid correlationId)
        : base(reservationId.Value, version, correlationId)
    {
        ReservationId = reservationId;
        OrderId = orderId;
        Items = items;
        FailedProductId = failedProductId;
        RequestedQuantity = requestedQuantity;
        AvailableQuantity = availableQuantity;
    }
}
