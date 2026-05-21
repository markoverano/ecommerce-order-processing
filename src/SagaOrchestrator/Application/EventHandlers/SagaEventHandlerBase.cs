using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Logging;
using SagaOrchestrator.Application.Services;
using SagaOrchestrator.Domain.Aggregates;
using SagaOrchestrator.Domain.Exceptions;
using SagaOrchestrator.Domain.Repositories;

namespace SagaOrchestrator.Application.EventHandlers;

public abstract class SagaEventHandlerBase<TEvent> : ISagaEventHandler
    where TEvent : DomainEvent
{
    protected readonly ISagaRepository Repository;
    protected readonly ISagaCommandPublisher CommandPublisher;
    protected readonly ILogger Logger;

    protected SagaEventHandlerBase(ISagaRepository repository, ISagaCommandPublisher commandPublisher, ILogger logger)
    {
        Repository = repository;
        CommandPublisher = commandPublisher;
        Logger = logger;
    }

    public string EventTypeName => typeof(TEvent).Name;
    public Type EventType => typeof(TEvent);

    public Task HandleAsync(DomainEvent evt, CancellationToken cancellationToken)
        => HandleAsync((TEvent)evt, cancellationToken);

    protected abstract Task HandleAsync(TEvent evt, CancellationToken cancellationToken);

    protected async Task<OrderProcessingSaga> LoadSagaOrThrowAsync(OrderId orderId, CancellationToken cancellationToken) =>
        await Repository.GetByOrderIdAsync(orderId, cancellationToken)
            ?? throw new SagaNotFoundException(orderId.Value);
}
