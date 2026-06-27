using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Shipping;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class ShipmentCreatedHandler : IAnalyticsEventHandler
{
    private readonly IShippingMetricRepository _shippingMetricRepository;
    private readonly ILogger<ShipmentCreatedHandler> _logger;

    public string EventTypeName => nameof(ShipmentCreated);
    public Type EventType => typeof(ShipmentCreated);

    public ShipmentCreatedHandler(
        IShippingMetricRepository shippingMetricRepository,
        ILogger<ShipmentCreatedHandler> logger)
    {
        _shippingMetricRepository = shippingMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((ShipmentCreated)evt, cancellationToken);

    private async Task HandleAsync(ShipmentCreated @event, CancellationToken cancellationToken)
    {
        var shipmentId = @event.ShipmentId.Value;

        var metric = ShippingMetric.Create(shipmentId, carrier: null, @event.TrackingNumber);
        await _shippingMetricRepository.AddAsync(metric, cancellationToken);
        await _shippingMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("ShipmentCreated projection written for shipment {ShipmentId}", shipmentId);
    }
}
