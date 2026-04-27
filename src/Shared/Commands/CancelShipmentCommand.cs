using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;

namespace ECommerceOrderProcessing.Shared.Commands;

/// <summary>Instructs Shipping Service to cancel a FedEx shipment during saga compensation.</summary>
public sealed record CancelShipmentCommand : IRequest<ServiceResponse<bool>>
{
    public ShipmentId ShipmentId { get; init; }
    public OrderId OrderId { get; init; }
    public Guid CorrelationId { get; init; }

    public CancelShipmentCommand(ShipmentId shipmentId, OrderId orderId, Guid correlationId)
    {
        ShipmentId = shipmentId;
        OrderId = orderId;
        CorrelationId = correlationId;
    }
}
