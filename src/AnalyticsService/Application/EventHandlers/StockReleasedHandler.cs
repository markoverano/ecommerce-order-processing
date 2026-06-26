using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Inventory;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class StockReleasedHandler : IAnalyticsEventHandler
{
    private readonly IInventoryMetricRepository _inventoryMetricRepository;
    private readonly ILogger<StockReleasedHandler> _logger;

    public string EventTypeName => nameof(StockReleased);
    public Type EventType => typeof(StockReleased);

    public StockReleasedHandler(
        IInventoryMetricRepository inventoryMetricRepository,
        ILogger<StockReleasedHandler> logger)
    {
        _inventoryMetricRepository = inventoryMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((StockReleased)evt, cancellationToken);

    private async Task HandleAsync(StockReleased @event, CancellationToken cancellationToken)
    {
        var reservationId = @event.ReservationId.Value;
        var releasedAt = @event.Timestamp.UtcDateTime;

        var metric = await _inventoryMetricRepository.GetByReservationIdAsync(reservationId, cancellationToken);
        if (metric is null)
        {
            _logger.LogWarning("StockReleased received for unknown reservation {ReservationId}", reservationId);
            return;
        }

        metric.ReleasedAt = releasedAt;
        metric.DurationHours = (int)Math.Ceiling((releasedAt - metric.ReservedAt).TotalHours);
        await _inventoryMetricRepository.UpdateAsync(metric, cancellationToken);
        await _inventoryMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("StockReleased projection updated for reservation {ReservationId}", reservationId);
    }
}
