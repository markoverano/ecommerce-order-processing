namespace ShippingService.Application.DTOs;

public sealed record ShipmentDto(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    string Status,
    string? TrackingNumber,
    string DestinationAddress,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
