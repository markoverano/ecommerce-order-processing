using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Payment;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class PaymentRefundedHandler : IAnalyticsEventHandler
{
    private readonly IPaymentMetricRepository _paymentMetricRepository;
    private readonly ISalesSummaryRepository _salesSummaryRepository;
    private readonly ICustomerMetricRepository _customerMetricRepository;
    private readonly IOrderMetricRepository _orderMetricRepository;
    private readonly ILogger<PaymentRefundedHandler> _logger;

    public string EventTypeName => nameof(PaymentRefunded);
    public Type EventType => typeof(PaymentRefunded);

    public PaymentRefundedHandler(
        IPaymentMetricRepository paymentMetricRepository,
        ISalesSummaryRepository salesSummaryRepository,
        ICustomerMetricRepository customerMetricRepository,
        IOrderMetricRepository orderMetricRepository,
        ILogger<PaymentRefundedHandler> logger)
    {
        _paymentMetricRepository = paymentMetricRepository;
        _salesSummaryRepository = salesSummaryRepository;
        _customerMetricRepository = customerMetricRepository;
        _orderMetricRepository = orderMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((PaymentRefunded)evt, cancellationToken);

    private async Task HandleAsync(PaymentRefunded @event, CancellationToken cancellationToken)
    {
        var paymentId = @event.PaymentId.Value;
        var orderId = @event.OrderId.Value;
        var refundAmount = @event.Amount.Amount;

        // Update existing record for this payment rather than creating a duplicate
        var paymentMetric = await _paymentMetricRepository.GetByPaymentIdAsync(paymentId, cancellationToken);
        if (paymentMetric is not null)
        {
            paymentMetric.Status = "Refunded";
            paymentMetric.RefundedAt = @event.Timestamp.UtcDateTime;
            await _paymentMetricRepository.UpdateAsync(paymentMetric, cancellationToken);
        }
        else
        {
            var newMetric = PaymentMetric.Create(orderId, paymentId, "Refunded", "Stripe", refundAmount);
            newMetric.RefundedAt = @event.Timestamp.UtcDateTime;
            await _paymentMetricRepository.AddAsync(newMetric, cancellationToken);
        }

        var orderMetric = await _orderMetricRepository.GetByOrderIdAsync(orderId, cancellationToken);
        if (orderMetric is not null)
        {
            var date = DateOnly.FromDateTime(orderMetric.CreatedAt);
            var summary = await _salesSummaryRepository.GetByDateAsync(date, cancellationToken);
            if (summary is not null)
            {
                summary.UpdateRevenue(-refundAmount, 0);
                await _salesSummaryRepository.UpdateAsync(summary, cancellationToken);
            }

            if (orderMetric.CustomerId is not null)
            {
                var customerMetric = await _customerMetricRepository.GetByCustomerIdAsync(orderMetric.CustomerId.Value, cancellationToken);
                if (customerMetric is not null)
                {
                    customerMetric.LifetimeValue -= refundAmount;
                    if (customerMetric.OrderCount > 0)
                        customerMetric.AverageOrderValue = customerMetric.LifetimeValue / customerMetric.OrderCount;
                    await _customerMetricRepository.UpdateAsync(customerMetric, cancellationToken);
                }
            }
        }

        await _paymentMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PaymentRefunded projection written for payment {PaymentId}", paymentId);
    }
}
