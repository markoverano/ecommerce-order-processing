using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Order;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class OrderFailedHandler : IAnalyticsEventHandler
{
    private readonly IOrderMetricRepository _orderMetricRepository;
    private readonly ISalesSummaryRepository _salesSummaryRepository;
    private readonly ILogger<OrderFailedHandler> _logger;

    public string EventTypeName => nameof(OrderFailed);
    public Type EventType => typeof(OrderFailed);

    public OrderFailedHandler(
        IOrderMetricRepository orderMetricRepository,
        ISalesSummaryRepository salesSummaryRepository,
        ILogger<OrderFailedHandler> logger)
    {
        _orderMetricRepository = orderMetricRepository;
        _salesSummaryRepository = salesSummaryRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((OrderFailed)evt, cancellationToken);

    private async Task HandleAsync(OrderFailed @event, CancellationToken cancellationToken)
    {
        var orderId = @event.OrderId.Value;
        var metric = await _orderMetricRepository.GetByOrderIdAsync(orderId, cancellationToken);

        if (metric is null)
        {
            _logger.LogWarning("OrderFailed received for unknown order {OrderId}", orderId);
            return;
        }

        metric.Status = "Failed";
        await _orderMetricRepository.UpdateAsync(metric, cancellationToken);

        var date = DateOnly.FromDateTime(metric.CreatedAt);
        var summary = await _salesSummaryRepository.GetByDateAsync(date, cancellationToken);
        if (summary is not null)
        {
            summary.UpdateRevenue(-metric.OrderValue, -1);
            await _salesSummaryRepository.UpdateAsync(summary, cancellationToken);
        }

        await _orderMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("OrderFailed projection updated for order {OrderId}", orderId);
    }
}
