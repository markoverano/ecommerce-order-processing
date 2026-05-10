using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ShippingService.Domain.Exceptions;

public sealed class ShipmentProcessingException : Exception
{
    public ShipmentProcessingException(string message) : base(message) { }
    public ShipmentProcessingException(string message, Exception inner) : base(message, inner) { }
}

public sealed class ShipmentNotFoundException : Exception
{
    public ShipmentNotFoundException(ShipmentId shipmentId)
        : base($"Shipment {shipmentId} was not found.") { }

    public ShipmentNotFoundException(Guid id)
        : base($"Shipment {id} was not found.") { }
}
