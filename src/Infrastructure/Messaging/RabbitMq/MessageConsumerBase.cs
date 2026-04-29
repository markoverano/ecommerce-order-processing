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
            try
            {
                var body = Encoding.UTF8.GetString(args.Body.Span);
                var eventType = args.BasicProperties.Type ?? string.Empty;
                await HandleMessageAsync(eventType, body, stoppingToken);
                _channel.BasicAck(args.DeliveryTag, multiple: false);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Unhandled error processing message from queue {Queue}", QueueName);
                _channel.BasicNack(args.DeliveryTag, multiple: false, requeue: false);
            }
        };

        _channel.BasicConsume(QueueName, autoAck: false, consumer: consumer);
        return Task.CompletedTask;
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
