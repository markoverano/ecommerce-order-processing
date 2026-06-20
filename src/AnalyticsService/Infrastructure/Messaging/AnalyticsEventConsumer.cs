using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Infrastructure.Messaging;
using ECommerceOrderProcessing.Shared.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace AnalyticsService.Infrastructure.Messaging;

public class AnalyticsEventConsumer : MessageConsumerBase
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AnalyticsEventConsumer> _logger;

    public AnalyticsEventConsumer(
        IConnectionFactory connectionFactory,
        IServiceProvider serviceProvider,
        ILogger<AnalyticsEventConsumer> logger)
        : base(connectionFactory, logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override string QueueName => "analytics-events-queue";

    protected override IEnumerable<string> RoutingKeys => new[]
    {
        "order.*.created",
        "order.*.confirmed",
        "order.*.failed",
        "order.*.compensated",
        "payment.*.processed",
        "payment.*.failed",
        "payment.*.refunded",
        "inventory.*.reserved",
        "inventory.*.released",
        "inventory.*.out-of-stock",
        "shipping.*.created",
        "shipping.*.dispatched",
        "shipping.*.delivered",
        "shipping.*.failed",
        "shipping.*.cancelled",
        "notification.*.sent",
        "notification.*.delivered",
        "notification.*.failed"
    };

    protected override string ExchangeName => "order.events";

    protected override async Task HandleMessageAsync(string message, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<AnalyticsEventDispatcher>();
            await dispatcher.DispatchAsync(message, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing analytics event: {Message}", message);
            throw;
        }
    }
}
