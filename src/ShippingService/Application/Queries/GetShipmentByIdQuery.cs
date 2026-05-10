using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;
using ShippingService.Application.DTOs;

namespace ShippingService.Application.Queries;

public sealed record GetShipmentByIdQuery(ShipmentId ShipmentId) : IRequest<ServiceResponse<ShipmentDto>>;
