using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Enums;
using PaymentService.Domain.Exceptions;
using PaymentService.Domain.Repositories;
using PaymentService.Domain.ValueObjects;
using PaymentService.Application.Repositories;

namespace PaymentService.Application.Webhooks;

/// <summary>
/// Processes validated Stripe webhook events. Deduplicates, routes to the Payment aggregate,
/// and persists via the write-side repository (which writes outbox in the same transaction).
/// </summary>
public sealed class StripeWebhookHandler
{
    private readonly IPaymentRepository _repository;
    private readonly IPaymentReadRepository _readRepository;
    private readonly IWebhookDeduplicator _deduplicator;
    private readonly ILogger<StripeWebhookHandler> _logger;

    public StripeWebhookHandler(
        IPaymentRepository repository,
        IPaymentReadRepository readRepository,
        IWebhookDeduplicator deduplicator,
        ILogger<StripeWebhookHandler> logger)
    {
        _repository = repository;
        _readRepository = readRepository;
        _deduplicator = deduplicator;
        _logger = logger;
    }

    public async Task HandleAsync(string webhookId, string eventType, string chargeId, string? failureMessage, Guid correlationId, CancellationToken cancellationToken)
    {
        if (await _deduplicator.IsProcessedAsync(webhookId, cancellationToken))
        {
            _logger.LogInformation("Stripe webhook {WebhookId} already processed, skipping.", webhookId);
            return;
        }

        switch (eventType)
        {
            case "charge.succeeded":
                await HandleChargeSucceededAsync(chargeId, correlationId, cancellationToken);
                break;
            case "charge.failed":
                await HandleChargeFailedAsync(chargeId, failureMessage ?? "Charge failed.", correlationId, cancellationToken);
                break;
            default:
                _logger.LogDebug("Ignoring Stripe webhook event type {EventType}", eventType);
                break;
        }

        await _deduplicator.MarkProcessedAsync(webhookId, eventType, cancellationToken);
    }

    private async Task HandleChargeSucceededAsync(string chargeId, Guid correlationId, CancellationToken ct)
    {
        var paymentId = await _readRepository.FindByStripeChargeIdAsync(chargeId, ct);
        if (paymentId is null)
        {
            _logger.LogWarning("No payment found for Stripe charge {ChargeId}", chargeId);
            return;
        }

        var payment = await _repository.GetByIdAsync(paymentId.Value, ct);
        if (payment is null || payment.Status != PaymentStatus.Pending)
            return;

        payment.MarkAsProcessed(StripeChargeId.From(chargeId), correlationId);
        await _repository.SaveAsync(payment, ct);

        _logger.LogInformation("Payment {PaymentId} reconciled as processed via Stripe webhook.", paymentId);
    }

    private async Task HandleChargeFailedAsync(string chargeId, string reason, Guid correlationId, CancellationToken ct)
    {
        var paymentId = await _readRepository.FindByStripeChargeIdAsync(chargeId, ct);
        if (paymentId is null)
        {
            _logger.LogWarning("No payment found for Stripe charge {ChargeId}", chargeId);
            return;
        }

        var payment = await _repository.GetByIdAsync(paymentId.Value, ct);
        if (payment is null || payment.Status != PaymentStatus.Pending)
            return;

        payment.MarkAsFailed(reason, correlationId);
        await _repository.SaveAsync(payment, ct);

        _logger.LogInformation("Payment {PaymentId} reconciled as failed via Stripe webhook.", paymentId);
    }
}
