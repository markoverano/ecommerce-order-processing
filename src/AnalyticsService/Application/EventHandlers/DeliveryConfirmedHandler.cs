using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Shipping;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class DeliveryConfirmedHandler : IAnalyticsEventHandler
{
    private readonly IShippingMetricRepository _shippingMetricRepository;
    private readonly IOrderMetricRepository _orderMetricRepository;
    private readonly ILogger<DeliveryConfirmedHandler> _logger;

    public string EventTypeName => nameof(DeliveryConfirmed);
    public Type EventType => typeof(DeliveryConfirmed);

    public DeliveryConfirmedHandler(
        IShippingMetricRepository shippingMetricRepository,
        IOrderMetricRepository orderMetricRepository,
        ILogger<DeliveryConfirmedHandler> logger)
    {
        _shippingMetricRepository = shippingMetricRepository;
        _orderMetricRepository = orderMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((DeliveryConfirmed)evt, cancellationToken);

    private async Task HandleAsync(DeliveryConfirmed @event, CancellationToken cancellationToken)
    {
        var shipmentId = @event.ShipmentId.Value;
        var orderId = @event.OrderId.Value;
        var deliveredAt = @event.DeliveredAt.UtcDateTime;

        var shippingMetric = await _shippingMetricRepository.GetByShipmentIdAsync(shipmentId, cancellationToken);
        if (shippingMetric is not null)
        {
            shippingMetric.DeliveredAt = deliveredAt;
            await _shippingMetricRepository.UpdateAsync(shippingMetric, cancellationToken);
        }
        else
        {
            _logger.LogWarning("DeliveryConfirmed received for unknown shipment {ShipmentId}", shipmentId);
        }

        var orderMetric = await _orderMetricRepository.GetByOrderIdAsync(orderId, cancellationToken);
        if (orderMetric is not null)
        {
            var days = (int)Math.Ceiling((deliveredAt - orderMetric.CreatedAt).TotalDays);
            orderMetric.FulfillmentDays = days;
            await _orderMetricRepository.UpdateAsync(orderMetric, cancellationToken);
        }

        await _shippingMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("DeliveryConfirmed projection updated for shipment {ShipmentId}", shipmentId);
    }
}
