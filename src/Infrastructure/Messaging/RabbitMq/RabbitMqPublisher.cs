using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ECommerceOrderProcessing.Shared.Domain;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace ECommerceOrderProcessing.Infrastructure.Messaging.RabbitMq;

public sealed class RabbitMqPublisher : IEventPublisher, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly ILogger<RabbitMqPublisher> _logger;
    private const string ExchangeName = "order.events";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public RabbitMqPublisher(IConnection connection, ILogger<RabbitMqPublisher> logger)
    {
        _connection = connection;
        _channel = _connection.CreateModel();
        _logger = logger;

        _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
    }

    public async Task PublishAsync<T>(T domainEvent, string routingKey, CancellationToken cancellationToken = default) where T : DomainEvent
    {
        var eventType = domainEvent.GetType().Name;
        var eventData = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), _jsonOptions);
        await PublishAsync(eventType, eventData, routingKey, cancellationToken);
    }

    public Task PublishAsync(string eventType, string eventData, string routingKey, CancellationToken cancellationToken = default)
    {
        var body = Encoding.UTF8.GetBytes(eventData);
        var properties = _channel.CreateBasicProperties();
        properties.Persistent = true;
        properties.ContentType = "application/json";
        properties.Type = eventType;
        properties.Headers = new Dictionary<string, object>
        {
            ["event-type"] = eventType
        };

        // Propagate W3C trace context so consumers can create child spans linked to the publisher's trace.
        var activity = Activity.Current;
        if (activity is not null)
        {
            var flags = activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded) ? "01" : "00";
            properties.Headers["traceparent"] = Encoding.UTF8.GetBytes(
                $"00-{activity.TraceId}-{activity.SpanId}-{flags}");
            if (!string.IsNullOrEmpty(activity.TraceStateString))
                properties.Headers["tracestate"] = Encoding.UTF8.GetBytes(activity.TraceStateString);
        }

        _channel.BasicPublish(
            exchange: ExchangeName,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body);

        _logger.LogDebug("Published {EventType} to exchange {Exchange} with routing key {RoutingKey}", eventType, ExchangeName, routingKey);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _channel.Close();
        _channel.Dispose();
        _connection.Close();
        _connection.Dispose();
    }
}
