using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Payment;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class PaymentProcessedHandler : IAnalyticsEventHandler
{
    private readonly IPaymentMetricRepository _paymentMetricRepository;
    private readonly ICustomerMetricRepository _customerMetricRepository;
    private readonly IOrderMetricRepository _orderMetricRepository;
    private readonly ILogger<PaymentProcessedHandler> _logger;

    public string EventTypeName => nameof(PaymentProcessed);
    public Type EventType => typeof(PaymentProcessed);

    public PaymentProcessedHandler(
        IPaymentMetricRepository paymentMetricRepository,
        ICustomerMetricRepository customerMetricRepository,
        IOrderMetricRepository orderMetricRepository,
        ILogger<PaymentProcessedHandler> logger)
    {
        _paymentMetricRepository = paymentMetricRepository;
        _customerMetricRepository = customerMetricRepository;
        _orderMetricRepository = orderMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((PaymentProcessed)evt, cancellationToken);

    private async Task HandleAsync(PaymentProcessed @event, CancellationToken cancellationToken)
    {
        var paymentId = @event.PaymentId.Value;
        var orderId = @event.OrderId.Value;
        var amount = @event.Amount.Amount;

        var paymentMetric = PaymentMetric.Create(orderId, paymentId, "Processed", "Stripe", amount);
        paymentMetric.ProcessedAt = @event.Timestamp.UtcDateTime;
        await _paymentMetricRepository.AddAsync(paymentMetric, cancellationToken);

        var orderMetric = await _orderMetricRepository.GetByOrderIdAsync(orderId, cancellationToken);
        if (orderMetric?.CustomerId is not null)
        {
            var customerId = orderMetric.CustomerId.Value;
            var customerMetric = await _customerMetricRepository.GetByCustomerIdAsync(customerId, cancellationToken)
                                 ?? CustomerMetric.Create(customerId);

            var isNew = customerMetric.Id == 0;
            customerMetric.OrderCount++;
            customerMetric.LifetimeValue += amount;
            customerMetric.LastOrderAt = @event.Timestamp.UtcDateTime;
            if (customerMetric.FirstOrderAt is null)
                customerMetric.FirstOrderAt = @event.Timestamp.UtcDateTime;
            if (customerMetric.OrderCount > 0)
                customerMetric.AverageOrderValue = customerMetric.LifetimeValue / customerMetric.OrderCount;
            customerMetric.RepeatRate = customerMetric.OrderCount > 1 ? 1m : 0m;

            if (isNew)
                await _customerMetricRepository.AddAsync(customerMetric, cancellationToken);
            else
                await _customerMetricRepository.UpdateAsync(customerMetric, cancellationToken);
        }

        await _paymentMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PaymentProcessed projection written for payment {PaymentId}", paymentId);
    }
}
