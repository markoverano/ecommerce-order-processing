using System.Text.Json;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using OrderService.Application.DTOs;
using OrderService.Application.Repositories;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Infrastructure.Repositories;

public sealed class EfCoreOrderReadRepository : IOrderReadRepository
{
    private readonly OrderDbContext _db;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public EfCoreOrderReadRepository(OrderDbContext db)
    {
        _db = db;
    }

    public async Task<OrderDto?> GetByIdAsync(OrderId orderId, CancellationToken cancellationToken = default)
    {
        var model = await _db.OrderViewModels
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == orderId.Value, cancellationToken);

        return model is null ? null : MapToDto(model);
    }

    public async Task<PagedResult<OrderDto>> GetAllAsync(int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var totalCount = await _db.OrderViewModels.CountAsync(cancellationToken);

        var items = await _db.OrderViewModels
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderDto>(
            items.Select(MapToDto).ToList().AsReadOnly(),
            totalCount,
            page,
            pageSize);
    }

    private static OrderDto MapToDto(OrderReadModel model)
    {
        var items = JsonSerializer.Deserialize<List<OrderItemJson>>(model.ItemsJson, _jsonOptions)
            ?? new List<OrderItemJson>();
        var address = JsonSerializer.Deserialize<ShippingAddressJson>(model.ShippingAddressJson, _jsonOptions)!;

        return new OrderDto(
            model.Id,
            model.CustomerId,
            model.Status,
            model.TotalAmount,
            model.Currency,
            items.Select(i => new OrderItemDto(i.ProductId, i.Quantity, i.UnitPrice, i.LineTotal, i.Currency)).ToList().AsReadOnly(),
            new ShippingAddressDto(address.Line1, address.Line2, address.City, address.State, address.PostalCode, address.CountryCode),
            model.CreatedAt,
            model.UpdatedAt);
    }

    // Private deserialization helpers matching the serialization shape written by the repository.
    private sealed record OrderItemJson(Guid ProductId, int Quantity, decimal UnitPrice, decimal LineTotal, string Currency);
    private sealed record ShippingAddressJson(string Line1, string? Line2, string City, string State, string PostalCode, string CountryCode);
}
