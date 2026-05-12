using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECommerceOrderProcessing.Infrastructure.Messaging.AzureServiceBus;

/// <summary>
/// Background service that polls the dead-letter queue for every subscription on the order-events topic.
/// Dead-lettered messages represent events that exhausted all delivery retries without being acknowledged.
/// Each message is logged at Error level with full context and then completed (removed) so the DLQ does
/// not grow unbounded and trigger Azure Service Bus quota limits.
/// </summary>
public sealed class AzureServiceBusDlqConsumer : BackgroundService
{
    private static readonly string[] Subscriptions =
    [
        "order-service",
        "payment-service",
        "inventory-service",
        "shipping-service",
        "notification-service",
        "saga-orchestrator"
    ];

    private const string TopicName = "order-events";
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReceiveWaitTime = TimeSpan.FromSeconds(1);

    private readonly ServiceBusClient _client;
    private readonly ILogger<AzureServiceBusDlqConsumer> _logger;

    public AzureServiceBusDlqConsumer(ServiceBusClient client, ILogger<AzureServiceBusDlqConsumer> logger)
    {
        _client = client;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var subscription in Subscriptions)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                await DrainDlqAsync(subscription, stoppingToken);
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task DrainDlqAsync(string subscription, CancellationToken cancellationToken)
    {
        await using var receiver = _client.CreateReceiver(
            TopicName,
            subscription,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });

        try
        {
            ServiceBusReceivedMessage? message;
            while ((message = await receiver.ReceiveMessageAsync(ReceiveWaitTime, cancellationToken)) is not null)
            {
                _logger.LogError(
                    "Dead-lettered message on subscription={Subscription} messageId={MessageId} eventType={EventType} deadLetterReason={DeadLetterReason} deadLetterDescription={DeadLetterDescription} correlationId={CorrelationId}",
                    subscription,
                    message.MessageId,
                    message.Subject ?? "unknown",
                    message.DeadLetterReason ?? "unknown",
                    message.DeadLetterErrorDescription ?? string.Empty,
                    message.CorrelationId ?? "unknown");

                await receiver.CompleteMessageAsync(message, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error draining DLQ for subscription={Subscription}", subscription);
        }
    }
}
