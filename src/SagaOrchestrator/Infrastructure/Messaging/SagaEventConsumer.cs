using ECommerceOrderProcessing.Infrastructure.Messaging.RabbitMq;
using ECommerceOrderProcessing.Shared.Events.Inventory;
using ECommerceOrderProcessing.Shared.Events.Notification;
using ECommerceOrderProcessing.Shared.Events.Order;
using ECommerceOrderProcessing.Shared.Events.Payment;
using ECommerceOrderProcessing.Shared.Events.Shipping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using SagaOrchestrator.Application.Services;

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
        var orchestrator = scope.ServiceProvider.GetRequiredService<SagaOrchestrationService>();

        switch (eventType)
        {
            case nameof(OrderCreated):
                await orchestrator.HandleOrderCreatedAsync(Deserialize<OrderCreated>(messageBody), cancellationToken);
                break;

            case nameof(PaymentProcessed):
                await orchestrator.HandlePaymentProcessedAsync(Deserialize<PaymentProcessed>(messageBody), cancellationToken);
                break;

            case nameof(PaymentFailed):
                await orchestrator.HandlePaymentFailedAsync(Deserialize<PaymentFailed>(messageBody), cancellationToken);
                break;

            case nameof(PaymentRefunded):
                await orchestrator.HandlePaymentRefundedAsync(Deserialize<PaymentRefunded>(messageBody), cancellationToken);
                break;

            case nameof(StockReserved):
                await orchestrator.HandleStockReservedAsync(Deserialize<StockReserved>(messageBody), cancellationToken);
                break;

            case nameof(OutOfStock):
                await orchestrator.HandleOutOfStockAsync(Deserialize<OutOfStock>(messageBody), cancellationToken);
                break;

            case nameof(StockReleased):
                await orchestrator.HandleStockReleasedAsync(Deserialize<StockReleased>(messageBody), cancellationToken);
                break;

            case nameof(ShipmentCreated):
                await orchestrator.HandleShipmentCreatedAsync(Deserialize<ShipmentCreated>(messageBody), cancellationToken);
                break;

            case nameof(ShipmentFailed):
                await orchestrator.HandleShipmentFailedAsync(Deserialize<ShipmentFailed>(messageBody), cancellationToken);
                break;

            case nameof(NotificationSent):
                await orchestrator.HandleNotificationSentAsync(Deserialize<NotificationSent>(messageBody), cancellationToken);
                break;

            default:
                Logger.LogWarning("Unhandled event type {EventType} on queue {Queue}", eventType, QueueName);
                break;
        }
    }
}
