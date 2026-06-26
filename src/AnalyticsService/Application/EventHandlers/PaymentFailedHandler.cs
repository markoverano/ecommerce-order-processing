using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Payment;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class PaymentFailedHandler : IAnalyticsEventHandler
{
    private readonly IPaymentMetricRepository _paymentMetricRepository;
    private readonly ISalesSummaryRepository _salesSummaryRepository;
    private readonly IOrderMetricRepository _orderMetricRepository;
    private readonly ILogger<PaymentFailedHandler> _logger;

    public string EventTypeName => nameof(PaymentFailed);
    public Type EventType => typeof(PaymentFailed);

    public PaymentFailedHandler(
        IPaymentMetricRepository paymentMetricRepository,
        ISalesSummaryRepository salesSummaryRepository,
        IOrderMetricRepository orderMetricRepository,
        ILogger<PaymentFailedHandler> logger)
    {
        _paymentMetricRepository = paymentMetricRepository;
        _salesSummaryRepository = salesSummaryRepository;
        _orderMetricRepository = orderMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((PaymentFailed)evt, cancellationToken);

    private async Task HandleAsync(PaymentFailed @event, CancellationToken cancellationToken)
    {
        var paymentId = @event.PaymentId.Value;
        var orderId = @event.OrderId.Value;

        var paymentMetric = PaymentMetric.Create(orderId, paymentId, "Failed", "Stripe");
        paymentMetric.FailedAt = @event.Timestamp.UtcDateTime;
        await _paymentMetricRepository.AddAsync(paymentMetric, cancellationToken);

        var orderMetric = await _orderMetricRepository.GetByOrderIdAsync(orderId, cancellationToken);
        if (orderMetric is not null)
        {
            var date = DateOnly.FromDateTime(orderMetric.CreatedAt);
            var summary = await _salesSummaryRepository.GetByDateAsync(date, cancellationToken);
            if (summary is not null)
            {
                summary.UpdateRevenue(-orderMetric.OrderValue, -1);
                await _salesSummaryRepository.UpdateAsync(summary, cancellationToken);
            }
        }

        await _paymentMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("PaymentFailed projection written for payment {PaymentId}", paymentId);
    }
}
