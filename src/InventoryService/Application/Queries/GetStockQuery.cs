using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using InventoryService.Application.DTOs;
using MediatR;

namespace InventoryService.Application.Queries;

public sealed record GetStockQuery(ProductId ProductId) : IRequest<ServiceResponse<StockDto>>;
