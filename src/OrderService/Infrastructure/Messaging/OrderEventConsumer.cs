using ECommerceOrderProcessing.Infrastructure.Messaging.RabbitMq;
using ECommerceOrderProcessing.Shared.Events.Order;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderService.Domain.Repositories;
using RabbitMQ.Client;

namespace OrderService.Infrastructure.Messaging;

/// <summary>
/// Listens for order lifecycle events published by the Saga Orchestrator and updates the
/// Order aggregate accordingly. Uses IServiceScopeFactory to get a fresh scoped DbContext per message.
/// </summary>
public sealed class OrderEventConsumer : MessageConsumerBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    protected override string QueueName => "order-service.saga-feedback";

    protected override IReadOnlyList<string> RoutingKeys => new[]
    {
        "order.confirmed",
        "order.failed",
        "order.compensated"
    };

    public OrderEventConsumer(IConnection connection, IServiceScopeFactory scopeFactory, ILogger<OrderEventConsumer> logger)
        : base(connection, logger)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task HandleMessageAsync(string eventType, string messageBody, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var repository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

        switch (eventType)
        {
            case nameof(OrderConfirmed):
                var confirmed = Deserialize<OrderConfirmed>(messageBody);
                await HandleConfirmedAsync(repository, confirmed, cancellationToken);
                break;

            case nameof(OrderFailed):
                var failed = Deserialize<OrderFailed>(messageBody);
                await HandleFailedAsync(repository, failed, cancellationToken);
                break;

            case nameof(OrderCompensated):
                var compensated = Deserialize<OrderCompensated>(messageBody);
                await HandleCompensatedAsync(repository, compensated, cancellationToken);
                break;

            default:
                Logger.LogWarning("Unhandled event type {EventType} on queue {Queue}", eventType, QueueName);
                break;
        }
    }

    private async Task HandleConfirmedAsync(IOrderRepository repository, OrderConfirmed evt, CancellationToken ct)
    {
        var order = await repository.GetByIdAsync(evt.OrderId, ct)
            ?? throw new KeyNotFoundException($"Order {evt.OrderId} not found during confirmation.");
        order.Confirm(evt.CorrelationId);
        await repository.SaveAsync(order, ct);
        Logger.LogInformation("Order {OrderId} confirmed.", evt.OrderId);
    }

    private async Task HandleFailedAsync(IOrderRepository repository, OrderFailed evt, CancellationToken ct)
    {
        var order = await repository.GetByIdAsync(evt.OrderId, ct)
            ?? throw new KeyNotFoundException($"Order {evt.OrderId} not found during failure.");
        order.Fail(evt.Reason, evt.CorrelationId);
        await repository.SaveAsync(order, ct);
        Logger.LogInformation("Order {OrderId} failed: {Reason}", evt.OrderId, evt.Reason);
    }

    private async Task HandleCompensatedAsync(IOrderRepository repository, OrderCompensated evt, CancellationToken ct)
    {
        var order = await repository.GetByIdAsync(evt.OrderId, ct)
            ?? throw new KeyNotFoundException($"Order {evt.OrderId} not found during compensation.");
        order.Compensate(evt.Reason, evt.CorrelationId);
        await repository.SaveAsync(order, ct);
        Logger.LogInformation("Order {OrderId} compensated: {Reason}", evt.OrderId, evt.Reason);
    }
}
