using ECommerceOrderProcessing.Shared.Models;
using MediatR;
using ShippingService.Application.DTOs;
using ShippingService.Application.Repositories;

namespace ShippingService.Application.Queries;

public sealed class GetShipmentQueryHandler : IRequestHandler<GetShipmentByIdQuery, ServiceResponse<ShipmentDto>>
{
    private readonly IShipmentReadRepository _readRepository;

    public GetShipmentQueryHandler(IShipmentReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<ServiceResponse<ShipmentDto>> Handle(GetShipmentByIdQuery query, CancellationToken cancellationToken)
    {
        var shipment = await _readRepository.GetByIdAsync(query.ShipmentId, cancellationToken);

        if (shipment is null)
            return ServiceResponse<ShipmentDto>.Failure("SHIPMENT_NOT_FOUND", $"Shipment {query.ShipmentId} was not found.");

        return ServiceResponse<ShipmentDto>.Success(shipment);
    }
}
