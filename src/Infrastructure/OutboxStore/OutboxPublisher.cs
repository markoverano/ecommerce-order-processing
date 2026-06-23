using ECommerceOrderProcessing.Infrastructure.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ECommerceOrderProcessing.Infrastructure.OutboxStore;

/// <summary>
/// Background service that polls the outbox table and publishes unpublished messages to the broker.
/// Runs within a scoped DI scope so each iteration gets its own DbContext.
/// </summary>
public sealed class OutboxPublisher : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxPublisher> _logger;
    private readonly int _batchSize;
    private readonly TimeSpan _pollingInterval;

    public OutboxPublisher(IServiceScopeFactory scopeFactory, ILogger<OutboxPublisher> logger, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _batchSize = configuration.GetValue("OutboxPublisher:BatchSize", 50);
        _pollingInterval = TimeSpan.FromSeconds(configuration.GetValue("OutboxPublisher:IntervalSeconds", 5));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in outbox publisher loop");
            }

            await Task.Delay(_pollingInterval, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var outboxStore = scope.ServiceProvider.GetRequiredService<IOutboxStore>();
        var publisher = scope.ServiceProvider.GetRequiredService<IOutboxEventPublisher>();

        var messages = await outboxStore.GetUnpublishedAsync(_batchSize, cancellationToken);
        if (messages.Count == 0)
            return;

        var publishTasks = messages.Select(async message =>
        {
            try
            {
                await publisher.PublishAsync(message.EventType, message.EventData, message.RoutingKey, cancellationToken);
                await outboxStore.MarkPublishedAsync(message.Id, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to publish outbox message {MessageId} of type {EventType}", message.Id, message.EventType);
            }
        });

        await Task.WhenAll(publishTasks);

        _logger.LogDebug("Outbox batch processed {Count} messages", messages.Count);
    }
}
