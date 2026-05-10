using ECommerceOrderProcessing.Shared.ValueObjects;
using ShippingService.Application.DTOs;

namespace ShippingService.Application.Repositories;

public interface IShipmentReadRepository
{
    Task<ShipmentDto?> GetByIdAsync(ShipmentId shipmentId, CancellationToken cancellationToken = default);
    Task<ShipmentId?> FindByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default);
}
