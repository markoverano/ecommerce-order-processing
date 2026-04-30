using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;
using OrderService.Application.DTOs;

namespace OrderService.Application.Queries;

/// <summary>Returns a single order view model by ID.</summary>
public sealed record GetOrderByIdQuery(OrderId OrderId, Guid CorrelationId) : IRequest<ServiceResponse<OrderDto>>;
