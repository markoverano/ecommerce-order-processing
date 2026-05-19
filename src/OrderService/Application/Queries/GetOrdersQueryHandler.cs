using ECommerceOrderProcessing.Shared.Auth;
using ECommerceOrderProcessing.Shared.Models;
using MediatR;
using OrderService.Application.DTOs;
using OrderService.Application.Repositories;

namespace OrderService.Application.Queries;

public sealed class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, ServiceResponse<PagedResult<OrderDto>>>
{
    private readonly IOrderReadRepository _readRepository;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public GetOrdersQueryHandler(IOrderReadRepository readRepository, ICurrentUserAccessor currentUserAccessor)
    {
        _readRepository = readRepository;
        _currentUserAccessor = currentUserAccessor;
    }

    public async Task<ServiceResponse<PagedResult<OrderDto>>> Handle(GetOrdersQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var user = _currentUserAccessor.GetCurrentUser();
        var isAdmin = user?.Roles.Contains(Roles.Admin) == true;
        // Admins see all orders; customers see only their own.
        var customerId = isAdmin ? (Guid?)null : user?.UserId.Value;

        var result = await _readRepository.GetAllAsync(page, pageSize, customerId, cancellationToken);
        return ServiceResponse<PagedResult<OrderDto>>.Success(result);
    }
}
