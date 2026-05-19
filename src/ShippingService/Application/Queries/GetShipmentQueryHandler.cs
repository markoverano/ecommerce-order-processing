using ECommerceOrderProcessing.Shared.Auth;
using ECommerceOrderProcessing.Shared.Models;
using MediatR;
using ShippingService.Application.DTOs;
using ShippingService.Application.Repositories;

namespace ShippingService.Application.Queries;

public sealed class GetShipmentQueryHandler : IRequestHandler<GetShipmentByIdQuery, ServiceResponse<ShipmentDto>>
{
    private readonly IShipmentReadRepository _readRepository;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public GetShipmentQueryHandler(IShipmentReadRepository readRepository, ICurrentUserAccessor currentUserAccessor)
    {
        _readRepository = readRepository;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<ServiceResponse<ShipmentDto>> Handle(GetShipmentByIdQuery query, CancellationToken cancellationToken)
    {
        var shipment = await _readRepository.GetByIdAsync(query.ShipmentId, cancellationToken);

        if (shipment is null)
            return ServiceResponse<ShipmentDto>.Failure("SHIPMENT_NOT_FOUND", $"Shipment {query.ShipmentId} was not found.");

        var user = _currentUserAccessor.GetCurrentUser();
        var isAdmin = user?.Roles.Contains(Roles.Admin) == true;

        if (!isAdmin && shipment.CustomerId != user?.UserId.Value)
            return ServiceResponse<ShipmentDto>.Failure("SHIPMENT_NOT_FOUND", $"Shipment {query.ShipmentId} was not found.");

        return ServiceResponse<ShipmentDto>.Success(shipment);
    }
}
