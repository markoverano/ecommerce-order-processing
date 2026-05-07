using ECommerceOrderProcessing.Shared.ValueObjects;

namespace PaymentService.Application.ExternalClients;

/// <summary>Abstracts the Stripe API so command handlers remain testable without real HTTP calls.</summary>
public interface IStripePaymentGateway
{
    Task<StripeChargeResult> ChargeAsync(string paymentMethodId, Money amount, Guid idempotencyKey, CancellationToken cancellationToken = default);
    Task<StripeRefundResult> RefundAsync(string chargeId, Money amount, CancellationToken cancellationToken = default);
}
