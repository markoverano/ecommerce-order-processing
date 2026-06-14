using ECommerceOrderProcessing.Shared.Domain;
using ECommerceOrderProcessing.Shared.Events.Inventory;
using ECommerceOrderProcessing.Shared.Events.Notification;
using ECommerceOrderProcessing.Shared.Events.Order;
using ECommerceOrderProcessing.Shared.Events.Payment;
using ECommerceOrderProcessing.Shared.Events.Shipping;
using ECommerceOrderProcessing.Shared.Events.Saga;

namespace ECommerceOrderProcessing.Infrastructure.Messaging;

/// <summary>
/// Builds partition-aware routing keys for domain events.
/// Routing keys include the aggregate ID to ensure ordering guarantees:
/// messages for the same aggregate are routed to the same queue partition.
/// Format: "{service}.{aggregateId:N}.{eventName}"
/// </summary>
public static class RoutingKeyBuilder
{
    public static string Build(DomainEvent evt) => evt switch
    {
        OrderCreated => $"order.{evt.AggregateId:N}.created",
        OrderConfirmed => $"order.{evt.AggregateId:N}.confirmed",
        OrderFailed => $"order.{evt.AggregateId:N}.failed",
        OrderCompensated => $"order.{evt.AggregateId:N}.compensated",

        PaymentProcessed => $"payment.{evt.AggregateId:N}.processed",
        PaymentFailed => $"payment.{evt.AggregateId:N}.failed",
        PaymentRefunded => $"payment.{evt.AggregateId:N}.refunded",

        StockReserved => $"inventory.{evt.AggregateId:N}.reserved",
        StockReleased => $"inventory.{evt.AggregateId:N}.released",
        OutOfStock => $"inventory.{evt.AggregateId:N}.out-of-stock",

        ShipmentCreated => $"shipping.{evt.AggregateId:N}.created",
        ShipmentDispatched => $"shipping.{evt.AggregateId:N}.dispatched",
        ShipmentFailed => $"shipping.{evt.AggregateId:N}.failed",
        ShipmentCancelled => $"shipping.{evt.AggregateId:N}.cancelled",
        DeliveryConfirmed => $"shipping.{evt.AggregateId:N}.delivery-confirmed",

        NotificationSent => $"notification.{evt.AggregateId:N}.sent",
        NotificationDelivered => $"notification.{evt.AggregateId:N}.delivered",
        NotificationFailed => $"notification.{evt.AggregateId:N}.failed",

        SagaStarted => $"saga.{evt.AggregateId:N}.started",
        SagaStepCompleted => $"saga.{evt.AggregateId:N}.step-completed",
        SagaCompleted => $"saga.{evt.AggregateId:N}.completed",
        SagaCompensated => $"saga.{evt.AggregateId:N}.compensated",

        _ => $"{evt.GetType().Name.ToLowerInvariant()}.{evt.AggregateId:N}.event"
    };

    public static IReadOnlyList<string> WildcardPatterns(string eventType) => eventType switch
    {
        nameof(OrderCreated) => new[] { "order.*.created" },
        nameof(OrderConfirmed) => new[] { "order.*.confirmed" },
        nameof(OrderFailed) => new[] { "order.*.failed" },
        nameof(OrderCompensated) => new[] { "order.*.compensated" },

        nameof(PaymentProcessed) => new[] { "payment.*.processed" },
        nameof(PaymentFailed) => new[] { "payment.*.failed" },
        nameof(PaymentRefunded) => new[] { "payment.*.refunded" },

        nameof(StockReserved) => new[] { "inventory.*.reserved" },
        nameof(StockReleased) => new[] { "inventory.*.released" },
        nameof(OutOfStock) => new[] { "inventory.*.out-of-stock" },

        nameof(ShipmentCreated) => new[] { "shipping.*.created" },
        nameof(ShipmentFailed) => new[] { "shipping.*.failed" },
        nameof(DeliveryConfirmed) => new[] { "shipping.*.delivery-confirmed" },

        nameof(NotificationSent) => new[] { "notification.*.sent" },
        nameof(NotificationFailed) => new[] { "notification.*.failed" },

        nameof(SagaStarted) => new[] { "saga.*.started" },
        nameof(SagaStepCompleted) => new[] { "saga.*.step-completed" },
        nameof(SagaCompleted) => new[] { "saga.*.completed" },
        nameof(SagaCompensated) => new[] { "saga.*.compensated" },

        _ => new[] { $"*.*.*" }
    };
}
