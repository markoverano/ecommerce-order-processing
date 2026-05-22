using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.Serialization;
using ECommerceOrderProcessing.Shared.Domain;
using Microsoft.Extensions.Logging;
using SagaOrchestrator.Application.EventHandlers;

namespace SagaOrchestrator.Infrastructure.Messaging;

public sealed class SagaEventDispatcher
{
    private readonly IReadOnlyDictionary<string, ISagaEventHandler> _handlers;
    private readonly ILogger<SagaEventDispatcher> _logger;

    public SagaEventDispatcher(IEnumerable<ISagaEventHandler> handlers, ILogger<SagaEventDispatcher> logger)
    {
        _handlers = handlers.ToDictionary(h => h.EventTypeName);
        _logger = logger;
    }

    public async Task DispatchAsync(string eventType, string messageBody, CancellationToken cancellationToken)
    {
        if (!_handlers.TryGetValue(eventType, out var handler))
        {
            _logger.LogWarning("No handler registered for saga event type {EventType}", eventType);
            return;
        }

        var evt = (DomainEvent?)JsonSerializer.Deserialize(messageBody, handler.EventType, InfrastructureJsonOptions.Default)
            ?? throw new InvalidOperationException($"Failed to deserialize {eventType}.");

        await handler.HandleAsync(evt, cancellationToken);
    }
}
