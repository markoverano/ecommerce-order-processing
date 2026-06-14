using ECommerceOrderProcessing.Infrastructure.Messaging.RabbitMq;
using ECommerceOrderProcessing.Shared.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace InventoryService.Infrastructure.Messaging;

/// <summary>
/// Consumes ReserveStockCommand and ReleaseStockCommand from RabbitMQ.
/// Uses IServiceScopeFactory to get a fresh scoped DbContext per message.
/// </summary>
public sealed class InventoryCommandConsumer : MessageConsumerBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    protected override string QueueName => "inventory-service.commands";

    protected override IReadOnlyList<string> RoutingKeys => new[]
    {
        "command.reserve-stock.*",
        "command.release-stock.*"
    };

    public InventoryCommandConsumer(
        IConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<InventoryCommandConsumer> logger)
        : base(connection, logger)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task HandleMessageAsync(string eventType, string messageBody, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<ISender>();

        switch (eventType)
        {
            case nameof(ReserveStockCommand):
                var reserveCommand = Deserialize<ReserveStockCommand>(messageBody);
                var reserveResult = await mediator.Send(reserveCommand, cancellationToken);
                if (!reserveResult.IsSuccess)
                    Logger.LogWarning("ReserveStockCommand failed: {Error}", reserveResult.Error?.Message);
                break;

            case nameof(ReleaseStockCommand):
                var releaseCommand = Deserialize<ReleaseStockCommand>(messageBody);
                var releaseResult = await mediator.Send(releaseCommand, cancellationToken);
                if (!releaseResult.IsSuccess)
                    Logger.LogWarning("ReleaseStockCommand failed: {Error}", releaseResult.Error?.Message);
                break;

            default:
                Logger.LogWarning("Unhandled command type {EventType} on queue {Queue}", eventType, QueueName);
                break;
        }
    }
}
