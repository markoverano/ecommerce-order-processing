using AnalyticsService.Domain.Entities;
using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Order;
using Microsoft.Extensions.Logging;

namespace AnalyticsService.Application.EventHandlers;

public sealed class OrderCreatedHandler : IAnalyticsEventHandler
{
    private readonly ISalesSummaryRepository _salesSummaryRepository;
    private readonly IOrderMetricRepository _orderMetricRepository;
    private readonly ILogger<OrderCreatedHandler> _logger;

    public string EventTypeName => nameof(OrderCreated);
    public Type EventType => typeof(OrderCreated);

    public OrderCreatedHandler(
        ISalesSummaryRepository salesSummaryRepository,
        IOrderMetricRepository orderMetricRepository,
        ILogger<OrderCreatedHandler> logger)
    {
        _salesSummaryRepository = salesSummaryRepository;
        _orderMetricRepository = orderMetricRepository;
        _logger = logger;
    }

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken) =>
        HandleAsync((OrderCreated)evt, cancellationToken);

    private async Task HandleAsync(OrderCreated @event, CancellationToken cancellationToken)
    {
        var orderId = @event.OrderId.Value;
        var customerId = @event.CustomerId.Value;
        var amount = @event.TotalAmount.Amount;
        var currency = @event.TotalAmount.Currency;
        var orderDate = @event.Timestamp.UtcDateTime;

        var metric = OrderMetric.Create(orderId, customerId, amount, currency);
        await _orderMetricRepository.AddAsync(metric, cancellationToken);

        var date = DateOnly.FromDateTime(orderDate);
        var summary = await _salesSummaryRepository.GetByDateAsync(date, cancellationToken)
                      ?? SalesSummary.Create(date);

        var isNew = summary.Id == 0;
        summary.UpdateRevenue(amount, 1);

        if (isNew)
            await _salesSummaryRepository.AddAsync(summary, cancellationToken);
        else
            await _salesSummaryRepository.UpdateAsync(summary, cancellationToken);

        await _salesSummaryRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("OrderCreated projection written for order {OrderId}", orderId);
    }
}
