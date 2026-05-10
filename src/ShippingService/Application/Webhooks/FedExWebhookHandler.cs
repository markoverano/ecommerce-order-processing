using Microsoft.Extensions.Logging;
using ShippingService.Domain.Enums;
using ShippingService.Domain.Repositories;
using ShippingService.Application.Repositories;

namespace ShippingService.Application.Webhooks;

/// <summary>
/// Processes validated FedEx webhook events. Deduplicates, routes to the Shipment aggregate,
/// and persists via the write-side repository (which writes outbox in the same transaction).
/// </summary>
public sealed class FedExWebhookHandler
{
    private readonly IShipmentRepository _repository;
    private readonly IShipmentReadRepository _readRepository;
    private readonly IWebhookDeduplicator _deduplicator;
    private readonly ILogger<FedExWebhookHandler> _logger;

    public FedExWebhookHandler(
        IShipmentRepository repository,
        IShipmentReadRepository readRepository,
        IWebhookDeduplicator deduplicator,
        ILogger<FedExWebhookHandler> logger)
    {
        _repository = repository;
        _readRepository = readRepository;
        _deduplicator = deduplicator;
        _logger = logger;
    }

    public async Task HandleAsync(
        string webhookId,
        string eventType,
        string trackingNumber,
        DateTimeOffset? eventTimestamp,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        if (await _deduplicator.IsProcessedAsync(webhookId, cancellationToken))
        {
            _logger.LogInformation("FedEx webhook {WebhookId} already processed, skipping.", webhookId);
            return;
        }

        switch (eventType)
        {
            case "dispatched":
            case "FX_DISPATCHED":
                await HandleDispatchedAsync(trackingNumber, eventTimestamp ?? DateTimeOffset.UtcNow, correlationId, cancellationToken);
                break;
            case "delivered":
            case "FX_DELIVERED":
                await HandleDeliveredAsync(trackingNumber, eventTimestamp ?? DateTimeOffset.UtcNow, correlationId, cancellationToken);
                break;
            default:
                _logger.LogDebug("Ignoring FedEx webhook event type {EventType}", eventType);
                break;
        }

        await _deduplicator.MarkProcessedAsync(webhookId, eventType, cancellationToken);
    }

    private async Task HandleDispatchedAsync(string trackingNumber, DateTimeOffset dispatchedAt, Guid correlationId, CancellationToken ct)
    {
        var shipmentId = await _readRepository.FindByTrackingNumberAsync(trackingNumber, ct);
        if (shipmentId is null)
        {
            _logger.LogWarning("No shipment found for FedEx tracking number {TrackingNumber}", trackingNumber);
            return;
        }

        var shipment = await _repository.GetByIdAsync(shipmentId.Value, ct);
        if (shipment is null || shipment.Status != ShipmentStatus.Created)
            return;

        shipment.Dispatch(dispatchedAt, correlationId);
        await _repository.SaveAsync(shipment, ct);

        _logger.LogInformation("Shipment {ShipmentId} dispatched via FedEx webhook.", shipmentId);
    }

    private async Task HandleDeliveredAsync(string trackingNumber, DateTimeOffset deliveredAt, Guid correlationId, CancellationToken ct)
    {
        var shipmentId = await _readRepository.FindByTrackingNumberAsync(trackingNumber, ct);
        if (shipmentId is null)
        {
            _logger.LogWarning("No shipment found for FedEx tracking number {TrackingNumber}", trackingNumber);
            return;
        }

        var shipment = await _repository.GetByIdAsync(shipmentId.Value, ct);
        if (shipment is null || shipment.Status is not (ShipmentStatus.Dispatched or ShipmentStatus.InTransit))
            return;

        shipment.ConfirmDelivery(deliveredAt, correlationId);
        await _repository.SaveAsync(shipment, ct);

        _logger.LogInformation("Shipment {ShipmentId} confirmed as delivered via FedEx webhook.", shipmentId);
    }
}
