using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;

namespace ECommerceOrderProcessing.Shared.Commands;

/// <summary>Instructs Shipping Service to book a FedEx shipment. Sent by the Saga Orchestrator after stock is reserved.</summary>
public sealed record CreateShipmentCommand : IRequest<ServiceResponse<ShipmentId>>
{
    public OrderId OrderId { get; init; }
    public CustomerId CustomerId { get; init; }
    public ShippingAddress ShippingAddress { get; init; }
    public IReadOnlyList<ShipmentItem> Items { get; init; }
    public Guid CorrelationId { get; init; }

    public CreateShipmentCommand(OrderId orderId, CustomerId customerId, ShippingAddress shippingAddress, IReadOnlyList<ShipmentItem> items, Guid correlationId)
    {
        OrderId = orderId;
        CustomerId = customerId;
        ShippingAddress = shippingAddress;
        Items = items;
        CorrelationId = correlationId;
    }
}

public sealed record ShipmentItem(ProductId ProductId, int Quantity, string Description);
