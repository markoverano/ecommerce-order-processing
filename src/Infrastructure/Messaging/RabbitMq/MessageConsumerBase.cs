using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ECommerceOrderProcessing.Infrastructure.Messaging.RabbitMq;

/// <summary>
/// Base class for all RabbitMQ message consumer workers.
/// Subclasses declare the queue name and routing keys to bind, then implement HandleMessageAsync.
/// </summary>
public abstract class MessageConsumerBase : BackgroundService
{
    private readonly IConnection _connection;
    protected readonly ILogger Logger;
    private IModel? _channel;
    private const string ExchangeName = "order.events";

    // Shared ActivitySource; registered via AddSource("ECommerce.Messaging") in each service's OpenTelemetry setup.
    public static readonly ActivitySource MessagingActivitySource = new("ECommerce.Messaging", "1.0.0");

    protected abstract string QueueName { get; }
    protected abstract IReadOnlyList<string> RoutingKeys { get; }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    protected MessageConsumerBase(IConnection connection, ILogger logger)
    {
        _connection = connection;
        Logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _channel = _connection.CreateModel();
        _channel.ExchangeDeclare(ExchangeName, ExchangeType.Topic, durable: true, autoDelete: false);
        _channel.QueueDeclare(QueueName, durable: true, exclusive: false, autoDelete: false);

        foreach (var key in RoutingKeys)
            _channel.QueueBind(QueueName, ExchangeName, key);

        _channel.BasicQos(0, 1, false);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.Received += async (_, args) =>
        {
            // Restore the publisher's trace context so this consumer span links to the originating trace.
            ActivityContext parentContext = ExtractTraceContext(args.BasicProperties.Headers);

            using var activity = MessagingActivitySource.StartActivity(
                $"rabbitmq.consume {QueueName}",
                ActivityKind.Consumer,
                parentContext);

            try
            {
                var body = Encoding.UTF8.GetString(args.Body.Span);
                var eventType = args.BasicProperties.Type ?? string.Empty;
                activity?.SetTag("messaging.system", "rabbitmq");
                activity?.SetTag("messaging.destination", QueueName);
                activity?.SetTag("messaging.operation", "receive");
                activity?.SetTag("messaging.rabbitmq.routing_key", eventType);
                await HandleMessageAsync(eventType, body, stoppingToken);
                _channel.BasicAck(args.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                Logger.LogError(ex, "Unhandled error processing message from queue {Queue}", QueueName);
                _channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
        };

        _channel.BasicConsume(QueueName, autoAck: false, consumer: consumer);
        return Task.CompletedTask;
    }

    private static ActivityContext ExtractTraceContext(IDictionary<string, object>? headers)
    {
        if (headers is null || !headers.TryGetValue("traceparent", out var traceparentObj))
            return default;

        var traceparent = traceparentObj is byte[] bytes
            ? Encoding.UTF8.GetString(bytes)
            : traceparentObj?.ToString();

        if (string.IsNullOrEmpty(traceparent))
            return default;

        // Attempt to extract tracestate as well.
        string? tracestate = null;
        if (headers.TryGetValue("tracestate", out var tracestateObj))
        {
            tracestate = tracestateObj is byte[] tsBytes
                ? Encoding.UTF8.GetString(tsBytes)
                : tracestateObj?.ToString();
        }

        return ActivityContext.TryParse(traceparent, tracestate, isRemote: true, out var context)
            ? context
            : default;
    }

    protected abstract Task HandleMessageAsync(string eventType, string messageBody, CancellationToken cancellationToken);

    protected T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, _jsonOptions)
        ?? throw new InvalidOperationException($"Failed to deserialize message as {typeof(T).Name}.");

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}
