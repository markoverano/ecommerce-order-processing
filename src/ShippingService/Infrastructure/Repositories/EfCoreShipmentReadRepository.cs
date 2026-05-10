using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using ShippingService.Application.DTOs;
using ShippingService.Application.Repositories;
using ShippingService.Infrastructure.Persistence;

namespace ShippingService.Infrastructure.Repositories;

public sealed class EfCoreShipmentReadRepository : IShipmentReadRepository
{
    private readonly ShippingDbContext _db;

    public EfCoreShipmentReadRepository(ShippingDbContext db)
    {
        _db = db;
    }

    public async Task<ShipmentDto?> GetByIdAsync(ShipmentId shipmentId, CancellationToken cancellationToken = default)
    {
        var model = await _db.ShipmentViewModels
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == shipmentId.Value, cancellationToken);

        return model is null ? null : MapToDto(model);
    }

    public async Task<ShipmentId?> FindByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken = default)
    {
        var id = await _db.ShipmentViewModels
            .AsNoTracking()
            .Where(x => x.TrackingNumber == trackingNumber)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return id.HasValue ? ShipmentId.From(id.Value) : null;
    }

    private static ShipmentDto MapToDto(ShipmentReadModel model) =>
        new(model.Id, model.OrderId, model.CustomerId, model.Status, model.TrackingNumber, model.DestinationAddress, model.CreatedAt, model.UpdatedAt);
}
