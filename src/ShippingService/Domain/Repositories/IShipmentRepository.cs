using ECommerceOrderProcessing.Shared.ValueObjects;
using ShippingService.Domain.Aggregates;

namespace ShippingService.Domain.Repositories;

public interface IShipmentRepository
{
    Task<Shipment?> GetByIdAsync(ShipmentId shipmentId, CancellationToken cancellationToken = default);
    Task SaveAsync(Shipment shipment, CancellationToken cancellationToken = default);
}
