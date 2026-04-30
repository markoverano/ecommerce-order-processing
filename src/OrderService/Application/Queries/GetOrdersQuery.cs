using ECommerceOrderProcessing.Shared.Models;
using MediatR;
using OrderService.Application.DTOs;

namespace OrderService.Application.Queries;

/// <summary>Returns a paginated list of order view models.</summary>
public sealed record GetOrdersQuery(int Page, int PageSize, Guid CorrelationId) : IRequest<ServiceResponse<PagedResult<OrderDto>>>;
