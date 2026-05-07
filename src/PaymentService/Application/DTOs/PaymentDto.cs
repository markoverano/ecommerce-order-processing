namespace PaymentService.Application.DTOs;

public sealed record PaymentDto(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    string Status,
    decimal Amount,
    string Currency,
    string? StripeChargeId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
