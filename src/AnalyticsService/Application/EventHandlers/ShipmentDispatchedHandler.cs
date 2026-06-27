using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Shipping;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class ShipmentDispatchedHandler : IAnalyticsEventHandler
{
    private readonly IShippingMetricRepository _shippingMetricRepository;
    private readonly ILogger<ShipmentDispatchedHandler> _logger;

    public string EventTypeName => nameof(ShipmentDispatched);
    public Type EventType => typeof(ShipmentDispatched);

    public ShipmentDispatchedHandler(
        IShippingMetricRepository shippingMetricRepository,
        ILogger<ShipmentDispatchedHandler> logger)
    {
        _shippingMetricRepository = shippingMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((ShipmentDispatched)evt, cancellationToken);

    private async Task HandleAsync(ShipmentDispatched @event, CancellationToken cancellationToken)
    {
        var shipmentId = @event.ShipmentId.Value;
        var metric = await _shippingMetricRepository.GetByShipmentIdAsync(shipmentId, cancellationToken);

        if (metric is null)
        {
            _logger.LogWarning("ShipmentDispatched received for unknown shipment {ShipmentId}", shipmentId);
            return;
        }

        metric.DispatchedAt = @event.DispatchedAt.UtcDateTime;
        metric.TrackingNumber = @event.TrackingNumber;
        await _shippingMetricRepository.UpdateAsync(metric, cancellationToken);
        await _shippingMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("ShipmentDispatched projection updated for shipment {ShipmentId}", shipmentId);
    }
}
