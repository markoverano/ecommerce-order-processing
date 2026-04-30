using ECommerceOrderProcessing.Shared.ValueObjects;

namespace OrderService.Application.DTOs;

public sealed record OrderDto(
    Guid Id,
    Guid CustomerId,
    string Status,
    decimal TotalAmount,
    string Currency,
    IReadOnlyList<OrderItemDto> Items,
    ShippingAddressDto ShippingAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);

public sealed record OrderItemDto(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal,
    string Currency);

public sealed record ShippingAddressDto(
    string Line1,
    string? Line2,
    string City,
    string State,
    string PostalCode,
    string CountryCode);
