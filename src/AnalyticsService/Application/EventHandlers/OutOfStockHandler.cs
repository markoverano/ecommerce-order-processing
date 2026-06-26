using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Inventory;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class OutOfStockHandler : IAnalyticsEventHandler
{
    private readonly IInventoryMetricRepository _inventoryMetricRepository;
    private readonly ILogger<OutOfStockHandler> _logger;

    public string EventTypeName => nameof(OutOfStock);
    public Type EventType => typeof(OutOfStock);

    public OutOfStockHandler(
        IInventoryMetricRepository inventoryMetricRepository,
        ILogger<OutOfStockHandler> logger)
    {
        _inventoryMetricRepository = inventoryMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((OutOfStock)evt, cancellationToken);

    private async Task HandleAsync(OutOfStock @event, CancellationToken cancellationToken)
    {
        var productId = @event.ProductId.Value;

        // Negative quantity records unfulfilled demand for out-of-stock incident tracking
        var metric = InventoryMetric.Create(productId, @event.AggregateId, -@event.RequestedQuantity);
        await _inventoryMetricRepository.AddAsync(metric, cancellationToken);
        await _inventoryMetricRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("OutOfStock incident recorded for product {ProductId} (requested {Requested}, available {Available})",
            productId, @event.RequestedQuantity, @event.AvailableQuantity);
    }
}
