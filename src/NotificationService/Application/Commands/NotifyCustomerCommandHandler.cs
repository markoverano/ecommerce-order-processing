using ECommerceOrderProcessing.Shared.Commands;
using ECommerceOrderProcessing.Shared.Models;
using ECommerceOrderProcessing.Shared.SignalR;
using ECommerceOrderProcessing.Shared.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using NotificationService.Application.ExternalClients;
using NotificationService.Application.Metrics;
using NotificationService.Application.Notifications;
using NotificationService.Application.Validation;
using NotificationService.Domain.Aggregates;
using NotificationService.Domain.Exceptions;
using NotificationService.Domain.Repositories;

namespace NotificationService.Application.Commands;

public sealed class NotifyCustomerCommandHandler : IRequestHandler<NotifyCustomerCommand, ServiceResponse<NotificationId>>
{
    private readonly INotificationRepository _repository;
    private readonly IMailgunNotificationClient _mailgun;
    private readonly ITwilioNotificationClient _twilio;
    private readonly NotifyCustomerCommandValidator _validator;
    private readonly IOrderStatusNotifier _notifier;
    private readonly ILogger<NotifyCustomerCommandHandler> _logger;

    public NotifyCustomerCommandHandler(
        INotificationRepository repository,
        IMailgunNotificationClient mailgun,
        ITwilioNotificationClient twilio,
        NotifyCustomerCommandValidator validator,
        IOrderStatusNotifier notifier,
        ILogger<NotifyCustomerCommandHandler> logger)
    {
        _repository = repository;
        _mailgun = mailgun;
        _twilio = twilio;
        _validator = validator;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<ServiceResponse<NotificationId>> Handle(NotifyCustomerCommand command, CancellationToken cancellationToken)
    {
        var validation = await _validator.ValidateAsync(command, cancellationToken);
        if (!validation.IsValid)
        {
            var errors = string.Join("; ", validation.Errors.Select(e => e.ErrorMessage));
            return ServiceResponse<NotificationId>.Failure("VALIDATION_FAILED", errors);
        }

        var hasEmail = command.TemplateData.TryGetValue("email", out var email) && !string.IsNullOrWhiteSpace(email);
        var hasPhone = command.TemplateData.TryGetValue("phone", out var phone) && !string.IsNullOrWhiteSpace(phone);

        if (!hasEmail && !hasPhone)
            return ServiceResponse<NotificationId>.Failure("NO_RECIPIENT",
                "TemplateData must contain 'email' or 'phone' to deliver the notification.");

        var channel = hasEmail ? "email" : "sms";
        var recipient = hasEmail ? email! : phone!;

        var notification = Notification.Create(
            command.OrderId,
            command.CustomerId,
            command.NotificationType,
            channel,
            recipient,
            command.TemplateData,
            command.CorrelationId);

        try
        {
            if (channel == "email")
            {
                var subject = BuildSubject(command.NotificationType);
                var body = BuildEmailBody(command.NotificationType, command.TemplateData);
                var result = await _mailgun.SendEmailAsync(email!, subject, body, notification.NotificationId.Value, cancellationToken);

                if (result.IsSuccess)
                    notification.MarkAsSent(result.MessageId, command.CorrelationId);
                else
                    notification.MarkAsFailed(result.ErrorMessage ?? "Mailgun rejected the message.", command.CorrelationId);
            }
            else
            {
                var message = BuildSmsBody(command.NotificationType, command.TemplateData);
                var result = await _twilio.SendSmsAsync(phone!, message, notification.NotificationId.Value, cancellationToken);

                if (result.IsSuccess)
                    notification.MarkAsSent(result.MessageSid, command.CorrelationId);
                else
                    notification.MarkAsFailed(result.ErrorMessage ?? "Twilio rejected the message.", command.CorrelationId);
            }
        }
        catch (NotificationException ex)
        {
            _logger.LogWarning(ex, "Notification provider rejected message for order {OrderId}: {Reason}", command.OrderId, ex.Message);
            notification.MarkAsFailed(ex.Message, command.CorrelationId);
        }

        await _repository.SaveAsync(notification, cancellationToken);

        await _notifier.NotifyAsync(new OrderStatusUpdate(
            command.OrderId.Value,
            "NotificationPending",
            notification.Status.ToString(),
            DateTimeOffset.UtcNow,
            command.CorrelationId), cancellationToken);

        if (notification.Status == NotificationService.Domain.Enums.NotificationStatus.Sent)
            NotificationMetrics.NotificationsSent.WithLabels(channel).Inc();
        else
            NotificationMetrics.NotificationsFailed.WithLabels(channel).Inc();

        _logger.LogInformation(
            "Notification {NotificationId} for order {OrderId} completed with status {Status}. CorrelationId={CorrelationId}",
            notification.NotificationId, command.OrderId, notification.Status, command.CorrelationId);

        return ServiceResponse<NotificationId>.Success(notification.NotificationId);
    }

    private static string BuildSubject(string notificationType) => notificationType switch
    {
        "OrderConfirmed" => "Your order has been confirmed",
        "ShipmentDispatched" => "Your order is on its way",
        "DeliveryConfirmed" => "Your order has been delivered",
        "OrderFailed" => "There was a problem with your order",
        _ => "Order update"
    };

    private static string BuildEmailBody(string notificationType, IReadOnlyDictionary<string, string> data)
    {
        var orderId = data.TryGetValue("orderId", out var oid) ? oid : "your order";
        return notificationType switch
        {
            "OrderConfirmed" => $"Your order {orderId} has been confirmed and is being processed.",
            "ShipmentDispatched" => $"Your order {orderId} has been dispatched.",
            "DeliveryConfirmed" => $"Your order {orderId} has been delivered.",
            "OrderFailed" => $"Unfortunately, your order {orderId} could not be completed.",
            _ => $"There is an update on your order {orderId}."
        };
    }

    private static string BuildSmsBody(string notificationType, IReadOnlyDictionary<string, string> data)
    {
        var orderId = data.TryGetValue("orderId", out var oid) ? oid : "your order";
        return notificationType switch
        {
            "OrderConfirmed" => $"Order {orderId} confirmed.",
            "ShipmentDispatched" => $"Order {orderId} dispatched.",
            "DeliveryConfirmed" => $"Order {orderId} delivered.",
            "OrderFailed" => $"Order {orderId} failed.",
            _ => $"Update on order {orderId}."
        };
    }
}
