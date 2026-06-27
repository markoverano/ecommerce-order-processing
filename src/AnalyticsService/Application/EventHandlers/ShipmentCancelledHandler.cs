using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Shipping;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class ShipmentCancelledHandler : IAnalyticsEventHandler
{
    private readonly IShippingMetricRepository _shippingMetricRepository;
    private readonly ILogger<ShipmentCancelledHandler> _logger;

    public string EventTypeName => nameof(ShipmentCancelled);
    public Type EventType => typeof(ShipmentCancelled);

    public ShipmentCancelledHandler(
        IShippingMetricRepository shippingMetricRepository,
        ILogger<ShipmentCancelledHandler> logger)
    {
        _shippingMetricRepository = shippingMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((ShipmentCancelled)evt, cancellationToken);

    private async Task HandleAsync(ShipmentCancelled @event, CancellationToken cancellationToken)
    {
        var shipmentId = @event.ShipmentId.Value;
        var metric = await _shippingMetricRepository.GetByShipmentIdAsync(shipmentId, cancellationToken);

        if (metric is null)
        {
            _logger.LogWarning("ShipmentCancelled received for unknown shipment {ShipmentId}", shipmentId);
            return;
        }

        metric.FailureReason = "Cancelled";
        await _shippingMetricRepository.UpdateAsync(metric, cancellationToken);
        await _shippingMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("ShipmentCancelled projection updated for shipment {ShipmentId}", shipmentId);
    }
}
