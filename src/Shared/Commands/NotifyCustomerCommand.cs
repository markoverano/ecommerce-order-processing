using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;

namespace ECommerceOrderProcessing.Shared.Commands;

/// <summary>Instructs Notification Service to send a customer notification. Sent by the Saga Orchestrator after shipment is created.</summary>
public sealed record NotifyCustomerCommand : IRequest<ServiceResponse<NotificationId>>
{
    public OrderId OrderId { get; init; }
    public CustomerId CustomerId { get; init; }
    public string NotificationType { get; init; }
    public IReadOnlyDictionary<string, string> TemplateData { get; init; }
    public Guid CorrelationId { get; init; }

    public NotifyCustomerCommand(OrderId orderId, CustomerId customerId, string notificationType, IReadOnlyDictionary<string, string> templateData, Guid correlationId)
    {
        OrderId = orderId;
        CustomerId = customerId;
        NotificationType = notificationType;
        TemplateData = templateData;
        CorrelationId = correlationId;
    }
}
