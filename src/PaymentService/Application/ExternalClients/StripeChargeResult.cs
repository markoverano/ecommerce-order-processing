namespace PaymentService.Application.ExternalClients;

public sealed record StripeChargeResult(bool IsSuccess, string? ChargeId, string? ErrorMessage);

public sealed record StripeRefundResult(bool IsSuccess, string? ErrorMessage);
