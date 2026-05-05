using ECommerceOrderProcessing.Shared.ValueObjects;
using PaymentService.Domain.Aggregates;

namespace PaymentService.Domain.Repositories;

/// <summary>Write-side repository for the Payment aggregate.</summary>
public interface IPaymentRepository
{
    Task<Payment?> GetByIdAsync(PaymentId paymentId, CancellationToken cancellationToken = default);
    Task SaveAsync(Payment payment, CancellationToken cancellationToken = default);
}
