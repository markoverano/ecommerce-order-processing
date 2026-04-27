using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;

namespace ECommerceOrderProcessing.Shared.Commands;

/// <summary>Instructs Inventory Service to hold stock for an order. Sent by the Saga Orchestrator after payment succeeds.</summary>
public sealed record ReserveStockCommand : IRequest<ServiceResponse<ReservationId>>
{
    public OrderId OrderId { get; init; }
    public IReadOnlyList<StockReservationItem> Items { get; init; }
    public Guid CorrelationId { get; init; }

    public ReserveStockCommand(OrderId orderId, IReadOnlyList<StockReservationItem> items, Guid correlationId)
    {
        OrderId = orderId;
        Items = items;
        CorrelationId = correlationId;
    }
}

public sealed record StockReservationItem(ProductId ProductId, int Quantity);
