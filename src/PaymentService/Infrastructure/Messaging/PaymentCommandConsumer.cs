using ECommerceOrderProcessing.Infrastructure.Messaging.RabbitMq;
using ECommerceOrderProcessing.Shared.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace PaymentService.Infrastructure.Messaging;

/// <summary>
/// Consumes ProcessPaymentCommand and RefundPaymentCommand from RabbitMQ.
/// Dispatches each command through MediatR so the Application layer handles them.
/// Uses IServiceScopeFactory to get a fresh scoped DbContext per message.
/// </summary>
public sealed class PaymentCommandConsumer : MessageConsumerBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    protected override string QueueName => "payment-service.commands";

    protected override IReadOnlyList<string> RoutingKeys => new[]
    {
        "command.process-payment.*",
        "command.refund-payment.*"
    };

    public PaymentCommandConsumer(
        IConnection connection,
        IServiceScopeFactory scopeFactory,
        ILogger<PaymentCommandConsumer> logger)
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
            case nameof(ProcessPaymentCommand):
                var processCommand = Deserialize<ProcessPaymentCommand>(messageBody);
                var processResult = await mediator.Send(processCommand, cancellationToken);
                if (!processResult.IsSuccess)
                    Logger.LogWarning("ProcessPaymentCommand failed: {Error}", processResult.Error?.Message);
                break;

            case nameof(RefundPaymentCommand):
                var refundCommand = Deserialize<RefundPaymentCommand>(messageBody);
                var refundResult = await mediator.Send(refundCommand, cancellationToken);
                if (!refundResult.IsSuccess)
                    Logger.LogWarning("RefundPaymentCommand failed: {Error}", refundResult.Error?.Message);
                break;

            default:
                Logger.LogWarning("Unhandled command type {EventType} on queue {Queue}", eventType, QueueName);
                break;
        }
    }
}
