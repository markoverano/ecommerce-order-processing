namespace OrderService.Api.Requests;

public sealed record CreateOrderRequest(
    IReadOnlyList<OrderItemRequestDto> Items,
    ShippingAddressRequestDto ShippingAddress);

public sealed record OrderItemRequestDto(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    string Currency);

public sealed record ShippingAddressRequestDto(
    string Line1,
    string? Line2,
    string City,
    string State,
    string PostalCode,
    string CountryCode);
