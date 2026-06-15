using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.EventStore;
using ECommerceOrderProcessing.Infrastructure.Messaging;
using ECommerceOrderProcessing.Infrastructure.OutboxStore;
using ECommerceOrderProcessing.Infrastructure.Serialization;
using ECommerceOrderProcessing.Infrastructure.Snapshots;
using ECommerceOrderProcessing.Shared.Events.Order;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using OrderService.Domain.Aggregates;
using OrderService.Domain.Enums;
using OrderService.Domain.Exceptions;
using OrderService.Domain.Repositories;
using OrderService.Infrastructure.Caching;
using OrderService.Infrastructure.Persistence;

namespace OrderService.Infrastructure.Repositories;

public sealed class EfCoreOrderRepository : IOrderRepository
{
    private const int SnapshotThreshold = 50;

    private readonly OrderDbContext _db;
    private readonly IEventStore _eventStore;
    private readonly IOutboxStore _outboxStore;
    private readonly ISnapshotStore _snapshotStore;
    private readonly IDistributedCache _cache;
    private readonly ILogger<EfCoreOrderRepository> _logger;

    public EfCoreOrderRepository(
        OrderDbContext db,
        IEventStore eventStore,
        IOutboxStore outboxStore,
        ISnapshotStore snapshotStore,
        IDistributedCache cache,
        ILogger<EfCoreOrderRepository> logger)
    {
        _db = db;
        _eventStore = eventStore;
        _outboxStore = outboxStore;
        _snapshotStore = snapshotStore;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Order?> GetByIdAsync(OrderId orderId, CancellationToken cancellationToken = default)
    {
        var snapshot = await _snapshotStore.GetLatestAsync(orderId.Value, cancellationToken);
        if (snapshot is not null)
        {
            var state = JsonSerializer.Deserialize<OrderSnapshotState>(snapshot.StateJson, InfrastructureJsonOptions.Default);
            if (state is not null)
            {
                var eventsSince = await _eventStore.GetEventsSinceAsync(orderId.Value, snapshot.Version, cancellationToken);
                var items = state.Items
                    .Select(i => new OrderItemData(ProductId.From(i.ProductId), i.Quantity, Money.Create(i.UnitPrice, i.Currency)))
                    .ToList()
                    .AsReadOnly();
                var address = ShippingAddress.Create(state.Line1, state.Line2, state.City, state.State, state.PostalCode, state.CountryCode);

                return Order.FromSnapshot(
                    state.OrderId,
                    CustomerId.From(state.CustomerId),
                    items,
                    Money.Create(state.TotalAmount, state.Currency),
                    address,
                    Enum.Parse<OrderStatus>(state.Status),
                    snapshot.Version,
                    eventsSince);
            }
        }

        var events = await _eventStore.GetEventsAsync(orderId.Value, cancellationToken);
        if (events.Count == 0)
            return null;

        return Order.Rehydrate(events);
    }

    public async Task SaveAsync(Order order, CancellationToken cancellationToken = default)
    {
        var uncommitted = order.UncommittedEvents;
        var expectedVersion = order.Version - uncommitted.Count;

        await _eventStore.AppendEventsAsync(order.Id, nameof(Order), uncommitted, expectedVersion, cancellationToken);

        foreach (var evt in uncommitted)
        {
            var outboxMessage = OutboxMessage.Create(
                evt.GetType().Name,
                JsonSerializer.Serialize(evt, evt.GetType(), InfrastructureJsonOptions.Default),
                GetRoutingKey(evt));
            await _outboxStore.AddAsync(outboxMessage, cancellationToken);
        }

        await UpdateViewModelAsync(order, uncommitted, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        await _cache.RemoveAsync(CachedOrderReadRepository.CacheKey(order.OrderId), cancellationToken);

        order.ClearUncommittedEvents();

        _logger.LogDebug("Saved order {OrderId}, version {Version}", order.OrderId, order.Version);

        if (order.Version % SnapshotThreshold == 0)
            await SaveSnapshotAsync(order, cancellationToken);
    }

    private async Task SaveSnapshotAsync(Order order, CancellationToken cancellationToken)
    {
        var state = new OrderSnapshotState(
            order.Id,
            order.CustomerId.Value,
            order.Items.Select(i => new OrderItemSnapshotData(i.ProductId.Value, i.Quantity, i.UnitPrice.Amount, i.UnitPrice.Currency)).ToList(),
            order.TotalAmount.Amount,
            order.TotalAmount.Currency,
            order.ShippingAddress.Line1,
            order.ShippingAddress.Line2,
            order.ShippingAddress.City,
            order.ShippingAddress.State,
            order.ShippingAddress.PostalCode,
            order.ShippingAddress.CountryCode,
            order.Status.ToString());

        var snapshot = new AggregateSnapshot
        {
            AggregateId = order.Id,
            AggregateType = nameof(Order),
            Version = order.Version,
            StateJson = JsonSerializer.Serialize(state, InfrastructureJsonOptions.Default),
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _snapshotStore.SaveAsync(snapshot, cancellationToken);
        _logger.LogDebug("Saved snapshot for order {OrderId} at version {Version}", order.OrderId, order.Version);
    }

    private static string GetRoutingKey(ECommerceOrderProcessing.Shared.Domain.DomainEvent evt) =>
        RoutingKeyBuilder.Build(evt);

    private async Task UpdateViewModelAsync(
        Order order,
        IReadOnlyList<ECommerceOrderProcessing.Shared.Domain.DomainEvent> events,
        CancellationToken cancellationToken)
    {
        foreach (var evt in events)
        {
            switch (evt)
            {
                case OrderCreated created:
                    await InsertViewModelAsync(order, created, cancellationToken);
                    break;
                case OrderConfirmed:
                case OrderFailed:
                case OrderCompensated:
                    await UpdateViewModelStatusAsync(order, cancellationToken);
                    break;
            }
        }
    }

    private Task InsertViewModelAsync(Order order, OrderCreated created, CancellationToken cancellationToken)
    {
        var itemDtos = created.Items.Select(i => new
        {
            productId = i.ProductId.Value,
            quantity = i.Quantity,
            unitPrice = i.UnitPrice.Amount,
            lineTotal = i.LineTotal.Amount,
            currency = i.UnitPrice.Currency
        });

        var addressDto = new
        {
            line1 = order.ShippingAddress.Line1,
            line2 = order.ShippingAddress.Line2,
            city = order.ShippingAddress.City,
            state = order.ShippingAddress.State,
            postalCode = order.ShippingAddress.PostalCode,
            countryCode = order.ShippingAddress.CountryCode
        };

        var viewModel = new OrderReadModel
        {
            Id = order.Id,
            CustomerId = order.CustomerId.Value,
            Status = order.Status.ToString(),
            TotalAmount = order.TotalAmount.Amount,
            Currency = order.TotalAmount.Currency,
            ItemsJson = JsonSerializer.Serialize(itemDtos, InfrastructureJsonOptions.Default),
            ShippingAddressJson = JsonSerializer.Serialize(addressDto, InfrastructureJsonOptions.Default),
            CreatedAt = created.Timestamp,
            UpdatedAt = null
        };

        _db.OrderViewModels.Add(viewModel);
        return Task.CompletedTask;
    }

    private async Task UpdateViewModelStatusAsync(Order order, CancellationToken cancellationToken)
    {
        var existing = await _db.OrderViewModels.FindAsync(new object[] { order.Id }, cancellationToken)
            ?? throw new OrderNotFoundException(order.Id);

        existing.Status = order.Status.ToString();
        existing.UpdatedAt = DateTimeOffset.UtcNow;
    }
}

file sealed record OrderSnapshotState(
    Guid OrderId,
    Guid CustomerId,
    List<OrderItemSnapshotData> Items,
    decimal TotalAmount,
    string Currency,
    string Line1,
    string? Line2,
    string City,
    string State,
    string PostalCode,
    string CountryCode,
    string Status);

file sealed record OrderItemSnapshotData(Guid ProductId, int Quantity, decimal UnitPrice, string Currency);
