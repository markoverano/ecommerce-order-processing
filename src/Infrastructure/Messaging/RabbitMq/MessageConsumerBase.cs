using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.Serialization;
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
            ActivityContext parentContext = TraceContextPropagator.Extract(args.BasicProperties.Headers);

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
                RabbitMqAckPolicy.Ack(_channel, args.DeliveryTag);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                Logger.LogError(ex, "Unhandled error processing message from queue {Queue}", QueueName);
                RabbitMqAckPolicy.Nack(_channel, args.DeliveryTag);
            }
        };

        _channel.BasicConsume(QueueName, autoAck: false, consumer: consumer);
        return Task.CompletedTask;
    }

    protected abstract Task HandleMessageAsync(string eventType, string messageBody, CancellationToken cancellationToken);

    protected T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, InfrastructureJsonOptions.Default)
        ?? throw new InvalidOperationException($"Failed to deserialize message as {typeof(T).Name}.");

    public override void Dispose()
    {
        _channel?.Close();
        _channel?.Dispose();
        base.Dispose();
    }
}
