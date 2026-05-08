namespace PaymentService.Api.Requests;

public sealed record ProcessPaymentRequest(
    Guid OrderId,
    Guid CustomerId,
    decimal Amount,
    string Currency,
    string PaymentMethodId);
