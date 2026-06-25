using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Order;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class OrderConfirmedHandler : IAnalyticsEventHandler
{
    private readonly IOrderMetricRepository _orderMetricRepository;
    private readonly ILogger<OrderConfirmedHandler> _logger;

    public string EventTypeName => nameof(OrderConfirmed);
    public Type EventType => typeof(OrderConfirmed);

    public OrderConfirmedHandler(
        IOrderMetricRepository orderMetricRepository,
        ILogger<OrderConfirmedHandler> logger)
    {
        _orderMetricRepository = orderMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((OrderConfirmed)evt, cancellationToken);

    private async Task HandleAsync(OrderConfirmed @event, CancellationToken cancellationToken)
    {
        var orderId = @event.OrderId.Value;
        var metric = await _orderMetricRepository.GetByOrderIdAsync(orderId, cancellationToken);

        if (metric is null)
        {
            _logger.LogWarning("OrderConfirmed received for unknown order {OrderId}", orderId);
            return;
        }

        metric.Status = "Confirmed";
        metric.ConfirmedAt = @event.Timestamp.UtcDateTime;

        await _orderMetricRepository.UpdateAsync(metric, cancellationToken);
        await _orderMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("OrderConfirmed projection updated for order {OrderId}", orderId);
    }
}
