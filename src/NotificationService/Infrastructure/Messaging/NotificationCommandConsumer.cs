using ECommerceOrderProcessing.Infrastructure.Messaging.RabbitMq;
using ECommerceOrderProcessing.Shared.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace NotificationService.Infrastructure.Messaging;

/// <summary>
/// Consumes NotifyCustomerCommand from RabbitMQ.
/// Dispatches each command through MediatR so the Application layer handles them.
/// Uses IServiceScopeFactory to get a fresh scoped DbContext per message.
/// </summary>
public sealed class NotificationCommandConsumer : MessageConsumerBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    protected override string QueueName => "notification-service.commands";

    protected override IReadOnlyList<string> RoutingKeys => new[]
    {
        "command.notify-customer"
    };

    public NotificationCommandConsumer(
        IConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationCommandConsumer> logger)
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
            case nameof(NotifyCustomerCommand):
                var command = Deserialize<NotifyCustomerCommand>(messageBody);
                var result = await mediator.Send(command, cancellationToken);
                if (!result.IsSuccess)
                    Logger.LogWarning("NotifyCustomerCommand failed: {Error}", result.Error?.Message);
                break;

            default:
                Logger.LogWarning("Unhandled command type {EventType} on queue {Queue}", eventType, QueueName);
                break;
        }
    }
}
