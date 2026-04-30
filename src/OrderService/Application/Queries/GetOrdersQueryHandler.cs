using ECommerceOrderProcessing.Shared.Models;
using MediatR;
using OrderService.Application.DTOs;
using OrderService.Application.Repositories;

namespace OrderService.Application.Queries;

public sealed class GetOrdersQueryHandler : IRequestHandler<GetOrdersQuery, ServiceResponse<PagedResult<OrderDto>>>
{
    private readonly IOrderReadRepository _readRepository;

    public GetOrdersQueryHandler(IOrderReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<ServiceResponse<PagedResult<OrderDto>>> Handle(GetOrdersQuery query, CancellationToken cancellationToken)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var result = await _readRepository.GetAllAsync(page, pageSize, cancellationToken);
        return ServiceResponse<PagedResult<OrderDto>>.Success(result);
    }
}
