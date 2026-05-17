using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Logging;
using SagaOrchestrator.Application.DTOs;
using SagaOrchestrator.Application.Repositories;
using SagaOrchestrator.Domain.Aggregates;
using SagaOrchestrator.Domain.Enums;
using SagaOrchestrator.Domain.Exceptions;
using SagaOrchestrator.Domain.Repositories;

namespace SagaOrchestrator.Application.Services;

public sealed class SagaAdminService
{
    private readonly ISagaRepository _repository;
    private readonly ISagaAdminReadRepository _readRepository;
    private readonly ISagaCommandPublisher _commandPublisher;
    private readonly ILogger<SagaAdminService> _logger;

    public SagaAdminService(
        ISagaRepository repository,
        ISagaAdminReadRepository readRepository,
        ISagaCommandPublisher commandPublisher,
        ILogger<SagaAdminService> logger)
    {
        _repository = repository;
        _readRepository = readRepository;
        _commandPublisher = commandPublisher;
        _logger = logger;
    }

    public Task<IReadOnlyList<SagaDto>> GetSagasByStatusAsync(
        string status,
        int limit,
        CancellationToken cancellationToken)
        => _readRepository.GetByStatusAsync(status, limit, cancellationToken);

    /// <summary>
    /// Re-issues the pending command for the saga's current step.
    /// Uses a fresh CorrelationId so the idempotency store does not suppress the retry.
    /// Only valid for sagas in Running or Compensating state.
    /// </summary>
    public async Task<string> RetryCurrentStepAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var saga = await _repository.GetByOrderIdAsync(OrderId.From(orderId), cancellationToken)
            ?? throw new SagaNotFoundException(orderId);

        if (saga.Status is not (SagaStatus.Running or SagaStatus.Compensating))
            throw new InvalidOperationException(
                $"Cannot retry saga in terminal state {saga.Status}. Only Running or Compensating sagas can be retried.");

        var correlationId = Guid.NewGuid();

        await ReissueCommandAsync(saga, correlationId, cancellationToken);

        _logger.LogWarning(
            "Admin retry issued for saga {SagaId} (order {OrderId}): step={Step}, newCorrelationId={CorrelationId}",
            saga.Id, orderId, saga.CurrentStep, correlationId);

        return $"Retry issued for step {saga.CurrentStep} with new correlationId {correlationId}.";
    }

    private async Task ReissueCommandAsync(OrderProcessingSaga saga, Guid correlationId, CancellationToken cancellationToken)
    {
        switch (saga.CurrentStep)
        {
            case SagaStep.PaymentPending:
                await _commandPublisher.PublishProcessPaymentAsync(
                    new ProcessPaymentCommand(saga.OrderId, saga.CustomerId, saga.TotalAmount, string.Empty, correlationId),
                    cancellationToken);
                break;

            case SagaStep.InventoryPending:
                var items = saga.Items
                    .Select(i => new StockReservationItem(i.ProductId, i.Quantity))
                    .ToList().AsReadOnly();
                await _commandPublisher.PublishReserveStockAsync(
                    new ReserveStockCommand(saga.OrderId, items, correlationId),
                    cancellationToken);
                break;

            case SagaStep.ShippingPending:
                var shipmentItems = saga.Items
                    .Select(i => new ShipmentItem(i.ProductId, i.Quantity, i.ProductId.Value.ToString()))
                    .ToList().AsReadOnly();
                await _commandPublisher.PublishCreateShipmentAsync(
                    new CreateShipmentCommand(saga.OrderId, saga.CustomerId, saga.ShippingAddress, shipmentItems, correlationId),
                    cancellationToken);
                break;

            case SagaStep.NotificationPending:
                var templateData = new Dictionary<string, string>
                {
                    ["orderId"] = saga.OrderId.Value.ToString()
                }.AsReadOnly();
                await _commandPublisher.PublishNotifyCustomerAsync(
                    new NotifyCustomerCommand(saga.OrderId, saga.CustomerId, "OrderConfirmed", templateData, correlationId),
                    cancellationToken);
                break;

            case SagaStep.InventoryCompensation:
                await _commandPublisher.PublishReleaseStockAsync(
                    new ReleaseStockCommand(
                        saga.ReservationId ?? throw new InvalidOperationException("ReservationId required for inventory compensation retry."),
                        saga.OrderId,
                        correlationId),
                    cancellationToken);
                break;

            case SagaStep.PaymentCompensation:
                await _commandPublisher.PublishRefundPaymentAsync(
                    new RefundPaymentCommand(
                        saga.PaymentId ?? throw new InvalidOperationException("PaymentId required for payment compensation retry."),
                        saga.OrderId,
                        saga.PaymentAmount ?? throw new InvalidOperationException("PaymentAmount required for payment compensation retry."),
                        saga.CompensationReason ?? "Admin-initiated retry.",
                        correlationId),
                    cancellationToken);
                break;

            default:
                throw new InvalidOperationException($"No command to re-issue for step {saga.CurrentStep}.");
        }
    }
}
