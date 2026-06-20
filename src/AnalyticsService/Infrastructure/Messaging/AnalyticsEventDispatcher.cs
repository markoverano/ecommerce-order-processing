using AnalyticsService.Domain.Repositories;
using ECommerceOrderProcessing.Infrastructure.Serialization;
using ECommerceOrderProcessing.Shared.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace AnalyticsService.Infrastructure.Messaging;

public class AnalyticsEventDispatcher
{
    private readonly IProcessedEventRepository _processedEventRepository;
    private readonly ILogger<AnalyticsEventDispatcher> _logger;

    public AnalyticsEventDispatcher(
        IProcessedEventRepository processedEventRepository,
        ILogger<AnalyticsEventDispatcher> logger)
    {
        _processedEventRepository = processedEventRepository;
        _logger = logger;
    }

    public async Task DispatchAsync(string messageJson, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(messageJson);
            var root = document.RootElement;

            if (!root.TryGetProperty("EventId", out var eventIdProp) ||
                !root.TryGetProperty("EventType", out var eventTypeProp) ||
                !root.TryGetProperty("CorrelationId", out var correlationIdProp))
            {
                _logger.LogWarning("Malformed event message: missing required properties");
                return;
            }

            var eventId = eventIdProp.GetString();
            var eventType = eventTypeProp.GetString();
            var correlationId = correlationIdProp.GetString();

            if (string.IsNullOrEmpty(eventId) || string.IsNullOrEmpty(eventType))
            {
                _logger.LogWarning("Malformed event message: null or empty event properties");
                return;
            }

            if (await _processedEventRepository.IsProcessedAsync(eventId, cancellationToken))
            {
                _logger.LogDebug("Event {EventId} already processed, skipping", eventId);
                return;
            }

            _logger.LogInformation("Processing event {EventType} with ID {EventId} and CorrelationId {CorrelationId}",
                eventType, eventId, correlationId);

            var processedEvent = AnalyticsService.Domain.Entities.ProcessedEvent.Create(eventId, eventType, correlationId ?? "");
            await _processedEventRepository.AddAsync(processedEvent, cancellationToken);
            await _processedEventRepository.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Successfully processed event {EventId}", eventId);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse event message");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error dispatching analytics event");
            throw;
        }
    }
}
