using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.EventStore;
using ECommerceOrderProcessing.Infrastructure.OutboxStore;
using ECommerceOrderProcessing.Infrastructure.Serialization;
using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Payment;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Logging;
using PaymentService.Domain.Aggregates;
using PaymentService.Domain.Events;
using PaymentService.Domain.Exceptions;
using PaymentService.Domain.Repositories;
using PaymentService.Infrastructure.Persistence;

namespace PaymentService.Infrastructure.Repositories;

public sealed class EfCorePaymentRepository : IPaymentRepository
{
    private readonly PaymentDbContext _db;
    private readonly IEventStore _eventStore;
    private readonly IOutboxStore _outboxStore;
    private readonly ILogger<EfCorePaymentRepository> _logger;

    public EfCorePaymentRepository(
        PaymentDbContext db,
        IEventStore eventStore,
        IOutboxStore outboxStore,
        ILogger<EfCorePaymentRepository> logger)
    {
        _db = db;
        _eventStore = eventStore;
        _outboxStore = outboxStore;
        _logger = logger;
    }

    public async Task<Payment?> GetByIdAsync(PaymentId paymentId, CancellationToken cancellationToken = default)
    {
        var events = await _eventStore.GetEventsAsync(paymentId.Value, cancellationToken);
        if (events.Count == 0)
            return null;

        return Payment.Rehydrate(events);
    }

    public async Task SaveAsync(Payment payment, CancellationToken cancellationToken = default)
    {
        var uncommitted = payment.UncommittedEvents;
        var expectedVersion = payment.Version - uncommitted.Count;

        await _eventStore.AppendEventsAsync(payment.Id, nameof(Payment), uncommitted, expectedVersion, cancellationToken);

        foreach (var evt in uncommitted)
        {
            var routingKey = GetRoutingKey(evt);
            if (routingKey is not null)
            {
                var outboxMessage = OutboxMessage.Create(
                    evt.GetType().Name,
                    JsonSerializer.Serialize(evt, evt.GetType(), InfrastructureJsonOptions.Default),
                    routingKey);
                await _outboxStore.AddAsync(outboxMessage, cancellationToken);
            }
        }

        await UpdateViewModelAsync(payment, uncommitted, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        payment.ClearUncommittedEvents();

        _logger.LogDebug("Saved payment {PaymentId}, version {Version}", payment.PaymentId, payment.Version);
    }

    // PaymentInitiated is internal to this service; downstream sagas only consume PaymentProcessed/Failed/Refunded.
    private static string? GetRoutingKey(DomainEvent evt) => evt switch
    {
        PaymentProcessed => "payment.processed",
        PaymentFailed => "payment.failed",
        PaymentRefunded => "payment.refunded",
        _ => null
    };

    private async Task UpdateViewModelAsync(
        Payment payment,
        IReadOnlyList<DomainEvent> events,
        CancellationToken cancellationToken)
    {
        foreach (var evt in events)
        {
            switch (evt)
            {
                case PaymentInitiated created:
                    InsertViewModel(payment, created);
                    break;
                case PaymentProcessed processed:
                    await UpdateViewModelStatusAsync(payment, processed.StripeChargeId, cancellationToken);
                    break;
                case PaymentFailed:
                case PaymentRefunded:
                    await UpdateViewModelStatusAsync(payment, null, cancellationToken);
                    break;
            }
        }
    }

    private void InsertViewModel(Payment payment, PaymentInitiated created)
    {
        _db.PaymentViewModels.Add(new PaymentReadModel
        {
            Id = payment.Id,
            OrderId = payment.OrderId.Value,
            CustomerId = payment.CustomerId.Value,
            Status = payment.Status.ToString(),
            Amount = payment.Amount.Amount,
            Currency = payment.Amount.Currency,
            StripeChargeId = null,
            CreatedAt = created.Timestamp,
            UpdatedAt = null
        });
    }

    private async Task UpdateViewModelStatusAsync(Payment payment, string? stripeChargeId, CancellationToken cancellationToken)
    {
        var existing = await _db.PaymentViewModels.FindAsync(new object[] { payment.Id }, cancellationToken)
            ?? throw new PaymentNotFoundException(payment.Id);

        existing.Status = payment.Status.ToString();
        existing.UpdatedAt = DateTimeOffset.UtcNow;

        if (stripeChargeId is not null)
            existing.StripeChargeId = stripeChargeId;
    }
}
