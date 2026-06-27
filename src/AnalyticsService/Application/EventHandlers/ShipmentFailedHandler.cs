using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Shipping;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class ShipmentFailedHandler : IAnalyticsEventHandler
{
    private readonly IShippingMetricRepository _shippingMetricRepository;
    private readonly ILogger<ShipmentFailedHandler> _logger;

    public string EventTypeName => nameof(ShipmentFailed);
    public Type EventType => typeof(ShipmentFailed);

    public ShipmentFailedHandler(
        IShippingMetricRepository shippingMetricRepository,
        ILogger<ShipmentFailedHandler> logger)
    {
        _shippingMetricRepository = shippingMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((ShipmentFailed)evt, cancellationToken);

    private async Task HandleAsync(ShipmentFailed @event, CancellationToken cancellationToken)
    {
        var shipmentId = @event.ShipmentId.Value;
        var metric = await _shippingMetricRepository.GetByShipmentIdAsync(shipmentId, cancellationToken);

        if (metric is null)
        {
            _logger.LogWarning("ShipmentFailed received for unknown shipment {ShipmentId}", shipmentId);
            return;
        }

        metric.FailureReason = @event.Reason;
        await _shippingMetricRepository.UpdateAsync(metric, cancellationToken);
        await _shippingMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("ShipmentFailed projection updated for shipment {ShipmentId}", shipmentId);
    }
}
