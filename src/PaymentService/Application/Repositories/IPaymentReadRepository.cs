using ECommerceOrderProcessing.Shared.ValueObjects;
using PaymentService.Application.DTOs;

namespace PaymentService.Application.Repositories;

/// <summary>Read-side repository for denormalized payment view models.</summary>
public interface IPaymentReadRepository
{
    Task<PaymentDto?> GetByIdAsync(PaymentId paymentId, CancellationToken cancellationToken = default);
    Task<PaymentId?> FindByStripeChargeIdAsync(string stripeChargeId, CancellationToken cancellationToken = default);
}
