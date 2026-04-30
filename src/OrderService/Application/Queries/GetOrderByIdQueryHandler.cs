using ECommerceOrderProcessing.Shared.Models;
using MediatR;
using OrderService.Application.DTOs;
using OrderService.Application.Repositories;

namespace OrderService.Application.Queries;

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, ServiceResponse<OrderDto>>
{
    private readonly IOrderReadRepository _readRepository;

    public GetOrderByIdQueryHandler(IOrderReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<ServiceResponse<OrderDto>> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken)
    {
        var order = await _readRepository.GetByIdAsync(query.OrderId, cancellationToken);

        if (order is null)
            return ServiceResponse<OrderDto>.Failure("ORDER_NOT_FOUND", $"Order {query.OrderId} was not found.");

        return ServiceResponse<OrderDto>.Success(order);
    }
}
