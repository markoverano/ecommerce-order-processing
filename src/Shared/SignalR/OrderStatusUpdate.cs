namespace ECommerceOrderProcessing.Shared.SignalR;

/// <summary>Real-time push payload sent to browser clients via the OrderStatusHub.</summary>
public sealed record OrderStatusUpdate(
    Guid OrderId,
    string SagaStep,
    string Status,
    DateTimeOffset Timestamp,
    Guid CorrelationId);
