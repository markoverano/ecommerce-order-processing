using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.Messaging;
using ECommerceOrderProcessing.Infrastructure.OutboxStore;
using ECommerceOrderProcessing.Infrastructure.Serialization;
using ECommerceOrderProcessing.Shared.Events.Saga;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SagaOrchestrator.Domain.Aggregates;
using SagaOrchestrator.Domain.Enums;
using SagaOrchestrator.Domain.Repositories;
using SagaOrchestrator.Infrastructure.Persistence;

namespace SagaOrchestrator.Infrastructure.Repositories;

public sealed class EfCoreSagaRepository : ISagaRepository
{
    private readonly SagaDbContext _db;
    private readonly IOutboxStore _outboxStore;
    private readonly ILogger<EfCoreSagaRepository> _logger;

    public EfCoreSagaRepository(
        SagaDbContext db,
        IOutboxStore outboxStore,
        ILogger<EfCoreSagaRepository> logger)
    {
        _db = db;
        _outboxStore = outboxStore;
        _logger = logger;
    }

    public async Task<OrderProcessingSaga?> GetByOrderIdAsync(OrderId orderId, CancellationToken cancellationToken = default)
    {
        var state = await _db.SagaStates
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrderId == orderId.Value, cancellationToken);

        if (state is null)
            return null;

        return MapToDomain(state);
    }

    public async Task SaveAsync(OrderProcessingSaga saga, CancellationToken cancellationToken = default)
    {
        var uncommitted = saga.UncommittedEvents;

        var existing = await _db.SagaStates.FindAsync(new object[] { saga.Id }, cancellationToken);

        if (existing is null)
        {
            _db.SagaStates.Add(MapToState(saga));
        }
        else
        {
            existing.Status = saga.Status.ToString();
            existing.CurrentStep = saga.CurrentStep.ToString();
            existing.PaymentId = saga.PaymentId?.Value;
            existing.PaymentAmount = saga.PaymentAmount?.Amount;
            existing.PaymentCurrency = saga.PaymentAmount?.Currency;
            existing.ReservationId = saga.ReservationId?.Value;
            existing.CompensationReason = saga.CompensationReason;
            existing.Version = saga.Version;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        foreach (var evt in uncommitted)
        {
            var outboxMessage = OutboxMessage.Create(
                evt.GetType().Name,
                JsonSerializer.Serialize(evt, evt.GetType(), InfrastructureJsonOptions.Default),
                GetRoutingKey(evt));
            await _outboxStore.AddAsync(outboxMessage, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);

        saga.ClearUncommittedEvents();

        _logger.LogDebug("Saved saga {SagaId} for order {OrderId}, version {Version}", saga.Id, saga.OrderId, saga.Version);
    }

    private static string GetRoutingKey(ECommerceOrderProcessing.Shared.Domain.DomainEvent evt) =>
        RoutingKeyBuilder.Build(evt);

    private static OrderProcessingSaga MapToDomain(SagaState state)
    {
        var shippingAddress = JsonSerializer.Deserialize<ShippingAddressDto>(state.ShippingAddressJson, InfrastructureJsonOptions.Default)!;
        var items = JsonSerializer.Deserialize<List<OrderItemDto>>(state.ItemsJson, InfrastructureJsonOptions.Default)!;

        return OrderProcessingSaga.FromSnapshot(
            sagaId: state.SagaId,
            orderId: OrderId.From(state.OrderId),
            customerId: new CustomerId(state.CustomerId),
            status: Enum.Parse<SagaStatus>(state.Status),
            currentStep: Enum.Parse<SagaStep>(state.CurrentStep),
            totalAmount: Money.Create(state.TotalAmount, state.Currency),
            shippingAddress: ShippingAddress.Create(
                shippingAddress.Line1,
                shippingAddress.Line2,
                shippingAddress.City,
                shippingAddress.State,
                shippingAddress.PostalCode,
                shippingAddress.CountryCode),
            items: items
                .Select(i => new OrderItemData(new ProductId(i.ProductId), i.Quantity, Money.Create(i.UnitPrice, i.Currency)))
                .ToList()
                .AsReadOnly(),
            paymentId: state.PaymentId.HasValue ? PaymentId.From(state.PaymentId.Value) : null,
            paymentAmount: state.PaymentAmount.HasValue && state.PaymentCurrency is not null
                ? Money.Create(state.PaymentAmount.Value, state.PaymentCurrency)
                : null,
            reservationId: state.ReservationId.HasValue ? ReservationId.From(state.ReservationId.Value) : null,
            compensationReason: state.CompensationReason,
            version: state.Version);
    }

    private static SagaState MapToState(OrderProcessingSaga saga)
    {
        var addressDto = new ShippingAddressDto(
            saga.ShippingAddress.Line1,
            saga.ShippingAddress.Line2,
            saga.ShippingAddress.City,
            saga.ShippingAddress.State,
            saga.ShippingAddress.PostalCode,
            saga.ShippingAddress.CountryCode);

        var itemDtos = saga.Items
            .Select(i => new OrderItemDto(i.ProductId.Value, i.Quantity, i.UnitPrice.Amount, i.UnitPrice.Currency))
            .ToList();

        return new SagaState
        {
            SagaId = saga.Id,
            OrderId = saga.OrderId.Value,
            CustomerId = saga.CustomerId.Value,
            Status = saga.Status.ToString(),
            CurrentStep = saga.CurrentStep.ToString(),
            TotalAmount = saga.TotalAmount.Amount,
            Currency = saga.TotalAmount.Currency,
            ShippingAddressJson = JsonSerializer.Serialize(addressDto, InfrastructureJsonOptions.Default),
            ItemsJson = JsonSerializer.Serialize(itemDtos, InfrastructureJsonOptions.Default),
            PaymentId = saga.PaymentId?.Value,
            PaymentAmount = saga.PaymentAmount?.Amount,
            PaymentCurrency = saga.PaymentAmount?.Currency,
            ReservationId = saga.ReservationId?.Value,
            CompensationReason = saga.CompensationReason,
            Version = saga.Version,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
    }

    // Private DTOs for JSON serialization — not part of the domain model.
    private sealed record ShippingAddressDto(string Line1, string? Line2, string City, string State, string PostalCode, string CountryCode);
    private sealed record OrderItemDto(Guid ProductId, int Quantity, decimal UnitPrice, string Currency);
}
