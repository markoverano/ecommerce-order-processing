using ECommerceOrderProcessing.Infrastructure.Messaging.RabbitMq;
using ECommerceOrderProcessing.Shared.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace ShippingService.Infrastructure.Messaging;

/// <summary>
/// Consumes CreateShipmentCommand and CancelShipmentCommand from RabbitMQ.
/// Dispatches each command through MediatR so the Application layer handles them.
/// Uses IServiceScopeFactory to get a fresh scoped DbContext per message.
/// </summary>
public sealed class ShippingCommandConsumer : MessageConsumerBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    protected override string QueueName => "shipping-service.commands";

    protected override IReadOnlyList<string> RoutingKeys => new[]
    {
        "command.create-shipment.*",
        "command.cancel-shipment.*"
    };

    public ShippingCommandConsumer(
        IConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<ShippingCommandConsumer> logger)
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
            case nameof(CreateShipmentCommand):
                var createCommand = Deserialize<CreateShipmentCommand>(messageBody);
                var createResult = await mediator.Send(createCommand, cancellationToken);
                if (!createResult.IsSuccess)
                    Logger.LogWarning("CreateShipmentCommand failed: {Error}", createResult.Error?.Message);
                break;

            case nameof(CancelShipmentCommand):
                var cancelCommand = Deserialize<CancelShipmentCommand>(messageBody);
                var cancelResult = await mediator.Send(cancelCommand, cancellationToken);
                if (!cancelResult.IsSuccess)
                    Logger.LogWarning("CancelShipmentCommand failed: {Error}", cancelResult.Error?.Message);
                break;

            default:
                Logger.LogWarning("Unhandled command type {EventType} on queue {Queue}", eventType, QueueName);
                break;
        }
    }
}
