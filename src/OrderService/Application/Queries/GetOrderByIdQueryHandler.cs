using ECommerceOrderProcessing.Shared.Auth;
using ECommerceOrderProcessing.Shared.Models;
using MediatR;
using OrderService.Application.DTOs;
using OrderService.Application.Repositories;

namespace OrderService.Application.Queries;

public sealed class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, ServiceResponse<OrderDto>>
{
    private readonly IOrderReadRepository _readRepository;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public GetOrderByIdQueryHandler(IOrderReadRepository readRepository, ICurrentUserAccessor currentUserAccessor)
    {
        _readRepository = readRepository;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<ServiceResponse<OrderDto>> Handle(GetOrderByIdQuery query, CancellationToken cancellationToken)
    {
        var order = await _readRepository.GetByIdAsync(query.OrderId, cancellationToken);

        if (order is null)
            return ServiceResponse<OrderDto>.Failure("ORDER_NOT_FOUND", $"Order {query.OrderId} was not found.");

        var user = _currentUserAccessor.GetCurrentUser();
        var isAdmin = user?.Roles.Contains(Roles.Admin) == true;

        if (!isAdmin && order.CustomerId != user?.UserId.Value)
            // Return NOT_FOUND rather than FORBIDDEN to avoid leaking existence of another customer's order.
            return ServiceResponse<OrderDto>.Failure("ORDER_NOT_FOUND", $"Order {query.OrderId} was not found.");

        return ServiceResponse<OrderDto>.Success(order);
    }
}
