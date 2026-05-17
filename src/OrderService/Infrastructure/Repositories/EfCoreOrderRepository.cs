using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.EventStore;
using ECommerceOrderProcessing.Infrastructure.OutboxStore;
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
    // Write a snapshot every 50 events to bound rehydration cost without excessive snapshot storage.
    private const int SnapshotThreshold = 50;

    private readonly OrderDbContext _db;
    private readonly IEventStore _eventStore;
    private readonly IOutboxStore _outboxStore;
    private readonly ISnapshotStore _snapshotStore;
    private readonly IDistributedCache _cache;
    private readonly ILogger<EfCoreOrderRepository> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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
            var eventsSince = await _eventStore.GetEventsSinceAsync(orderId.Value, snapshot.Version, cancellationToken);
            return DeserializeSnapshot(snapshot, eventsSince);
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
                JsonSerializer.Serialize(evt, evt.GetType(), _jsonOptions),
                GetRoutingKey(evt));
            await _outboxStore.AddAsync(outboxMessage, cancellationToken);
        }

        await UpdateViewModelAsync(order, uncommitted, cancellationToken);

        if (order.Version > 0 && order.Version % SnapshotThreshold == 0)
            await _snapshotStore.SaveAsync(order.Id, nameof(Order), SerializeSnapshot(order), order.Version, cancellationToken);

        await _db.SaveChangesAsync(cancellationToken);

        await _cache.RemoveAsync(CachedOrderReadRepository.CacheKey(order.OrderId), cancellationToken);

        order.ClearUncommittedEvents();

        _logger.LogDebug("Saved order {OrderId}, version {Version}", order.OrderId, order.Version);
    }

    private static string GetRoutingKey(ECommerceOrderProcessing.Shared.Domain.DomainEvent evt) => evt switch
    {
        OrderCreated => "order.created",
        OrderConfirmed => "order.confirmed",
        OrderFailed => "order.failed",
        OrderCompensated => "order.compensated",
        _ => $"order.{evt.GetType().Name.ToLowerInvariant()}"
    };

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
            ItemsJson = JsonSerializer.Serialize(itemDtos, _jsonOptions),
            ShippingAddressJson = JsonSerializer.Serialize(addressDto, _jsonOptions),
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

    private static string SerializeSnapshot(Order order)
    {
        var payload = new OrderSnapshotPayload(
            order.Id,
            order.CustomerId.Value,
            order.Status.ToString(),
            order.TotalAmount.Amount,
            order.TotalAmount.Currency,
            order.Items.Select(i => new OrderSnapshotItem(i.ProductId.Value, i.Quantity, i.UnitPrice.Amount, i.UnitPrice.Currency)).ToList(),
            new OrderSnapshotAddress(
                order.ShippingAddress.Line1,
                order.ShippingAddress.Line2,
                order.ShippingAddress.City,
                order.ShippingAddress.State,
                order.ShippingAddress.PostalCode,
                order.ShippingAddress.CountryCode));

        return JsonSerializer.Serialize(payload, _jsonOptions);
    }

    private static Order DeserializeSnapshot(
        AggregateSnapshot snapshot,
        IReadOnlyList<ECommerceOrderProcessing.Shared.Domain.DomainEvent> eventsSince)
    {
        var payload = JsonSerializer.Deserialize<OrderSnapshotPayload>(snapshot.SnapshotData, _jsonOptions)!;

        var itemData = payload.Items
            .Select(i => new OrderItemData(new ProductId(i.ProductId), i.Quantity, Money.Create(i.UnitPrice, i.Currency)))
            .ToList()
            .AsReadOnly();

        var address = ShippingAddress.Create(
            payload.ShippingAddress.Line1,
            payload.ShippingAddress.Line2,
            payload.ShippingAddress.City,
            payload.ShippingAddress.State,
            payload.ShippingAddress.PostalCode,
            payload.ShippingAddress.CountryCode);

        return Order.FromSnapshot(
            orderId: payload.OrderId,
            customerId: new CustomerId(payload.CustomerId),
            itemData: itemData,
            totalAmount: Money.Create(payload.TotalAmount, payload.Currency),
            shippingAddress: address,
            status: Enum.Parse<OrderStatus>(payload.Status),
            snapshotVersion: snapshot.Version,
            eventsSinceSnapshot: eventsSince);
    }

    // Private DTOs for snapshot serialization — not part of the domain model.
    private sealed record OrderSnapshotPayload(
        Guid OrderId,
        Guid CustomerId,
        string Status,
        decimal TotalAmount,
        string Currency,
        List<OrderSnapshotItem> Items,
        OrderSnapshotAddress ShippingAddress);

    private sealed record OrderSnapshotItem(Guid ProductId, int Quantity, decimal UnitPrice, string Currency);

    private sealed record OrderSnapshotAddress(string Line1, string? Line2, string City, string State, string PostalCode, string CountryCode);
}
