using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Order;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class OrderCompensatedHandler : IAnalyticsEventHandler
{
    private readonly IOrderMetricRepository _orderMetricRepository;
    private readonly ILogger<OrderCompensatedHandler> _logger;

    public string EventTypeName => nameof(OrderCompensated);
    public Type EventType => typeof(OrderCompensated);

    public OrderCompensatedHandler(
        IOrderMetricRepository orderMetricRepository,
        ILogger<OrderCompensatedHandler> logger)
    {
        _orderMetricRepository = orderMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((OrderCompensated)evt, cancellationToken);

    private async Task HandleAsync(OrderCompensated @event, CancellationToken cancellationToken)
    {
        var orderId = @event.OrderId.Value;
        var metric = await _orderMetricRepository.GetByOrderIdAsync(orderId, cancellationToken);

        if (metric is null)
        {
            _logger.LogWarning("OrderCompensated received for unknown order {OrderId}", orderId);
            return;
        }

        metric.Status = "Compensated";
        await _orderMetricRepository.UpdateAsync(metric, cancellationToken);
        await _orderMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("OrderCompensated projection updated for order {OrderId}", orderId);
    }
}
