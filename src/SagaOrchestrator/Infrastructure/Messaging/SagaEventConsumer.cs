using ECommerceOrderProcessing.Infrastructure.Messaging.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
namespace SagaOrchestrator.Infrastructure.Messaging;

/// <summary>
/// Consumes all domain events that drive saga state transitions.
/// Uses IServiceScopeFactory to get a fresh scoped DbContext per message.
/// </summary>
public sealed class SagaEventConsumer : MessageConsumerBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    protected override string QueueName => "saga-orchestrator.events";

    protected override IReadOnlyList<string> RoutingKeys => new[]
    {
        "order.created",
        "payment.processed",
        "payment.failed",
        "payment.refunded",
        "inventory.stock-reserved",
        "inventory.out-of-stock",
        "inventory.stock-released",
        "shipping.shipment-created",
        "shipping.shipment-failed",
        "notification.notification-sent"
    };

    public SagaEventConsumer(
        IConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<SagaEventConsumer> logger)
        : base(connection, logger)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task HandleMessageAsync(string eventType, string messageBody, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<SagaEventDispatcher>();
        await dispatcher.DispatchAsync(eventType, messageBody, cancellationToken);
    }
}
