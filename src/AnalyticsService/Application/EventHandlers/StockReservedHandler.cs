using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Inventory;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class StockReservedHandler : IAnalyticsEventHandler
{
    private readonly IInventoryMetricRepository _inventoryMetricRepository;
    private readonly ILogger<StockReservedHandler> _logger;

    public string EventTypeName => nameof(StockReserved);
    public Type EventType => typeof(StockReserved);

    public StockReservedHandler(
        IInventoryMetricRepository inventoryMetricRepository,
        ILogger<StockReservedHandler> logger)
    {
        _inventoryMetricRepository = inventoryMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((StockReserved)evt, cancellationToken);

    private async Task HandleAsync(StockReserved @event, CancellationToken cancellationToken)
    {
        var reservationId = @event.ReservationId.Value;

        foreach (var item in @event.Items)
        {
            var metric = InventoryMetric.Create(item.ProductId.Value, reservationId, item.Quantity);
            await _inventoryMetricRepository.AddAsync(metric, cancellationToken);
        }

        await _inventoryMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("StockReserved projection written for reservation {ReservationId}", reservationId);
    }
}
