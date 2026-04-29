using System.Text.Json;
using Azure.Messaging.ServiceBus;
using ECommerceOrderProcessing.Shared.Domain;
using Microsoft.Extensions.Logging;

namespace ECommerceOrderProcessing.Infrastructure.Messaging.AzureServiceBus;

public sealed class AzureServiceBusPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ServiceBusClient _client;
    private readonly ILogger<AzureServiceBusPublisher> _logger;
    private const string TopicName = "order-events";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AzureServiceBusPublisher(ServiceBusClient client, ILogger<AzureServiceBusPublisher> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T domainEvent, string routingKey, CancellationToken cancellationToken = default) where T : DomainEvent
    {
        var eventType = domainEvent.GetType().Name;
        var eventData = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), _jsonOptions);
        await PublishAsync(eventType, eventData, routingKey, cancellationToken);
    }

    public async Task PublishAsync(string eventType, string eventData, string routingKey, CancellationToken cancellationToken = default)
    {
        await using var sender = _client.CreateSender(TopicName);
        var message = new ServiceBusMessage(eventData)
        {
            Subject = eventType,
            ContentType = "application/json",
            ApplicationProperties = { ["routing-key"] = routingKey }
        };

        await sender.SendMessageAsync(message, cancellationToken);
        _logger.LogDebug("Published {EventType} to Azure Service Bus topic {Topic}", eventType, TopicName);
    }

    public async ValueTask DisposeAsync() => await _client.DisposeAsync();
}
