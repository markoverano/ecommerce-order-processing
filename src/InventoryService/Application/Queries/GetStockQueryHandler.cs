using ECommerceOrderProcessing.Shared.Models;
using InventoryService.Application.DTOs;
using InventoryService.Application.Repositories;
using MediatR;

namespace InventoryService.Application.Queries;

public sealed class GetStockQueryHandler : IRequestHandler<GetStockQuery, ServiceResponse<StockDto>>
{
    private readonly IStockReadRepository _readRepository;

    public GetStockQueryHandler(IStockReadRepository readRepository)
    {
        _readRepository = readRepository;
    }

    public async Task<ServiceResponse<StockDto>> Handle(GetStockQuery query, CancellationToken cancellationToken)
    {
        var dto = await _readRepository.GetByProductIdAsync(query.ProductId, cancellationToken);
        return dto is null
            ? ServiceResponse<StockDto>.Failure("PRODUCT_NOT_FOUND", $"Product {query.ProductId} was not found.")
            : ServiceResponse<StockDto>.Success(dto);
    }
}
