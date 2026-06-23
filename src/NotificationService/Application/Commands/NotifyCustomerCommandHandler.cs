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

file static class NotificationChannels
{
    public const string Email = "email";
    public const string Sms = "sms";
}

file record NotificationTemplate(string Subject, string Body);

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
            return validation.ToFailureResponse<NotificationId>();

        var hasEmail = command.TemplateData.TryGetValue("email", out var email) && !string.IsNullOrWhiteSpace(email);
        var hasPhone = command.TemplateData.TryGetValue("phone", out var phone) && !string.IsNullOrWhiteSpace(phone);

        if (!hasEmail && !hasPhone)
            return ServiceResponse<NotificationId>.Failure("NO_RECIPIENT",
                "TemplateData must contain 'email' or 'phone' to deliver the notification.");

        var channel = hasEmail ? NotificationChannels.Email : NotificationChannels.Sms;
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
            var template = GetTemplate(channel, command.NotificationType);
            if (channel == NotificationChannels.Email)
            {
                var body = template.Body.Replace("{orderId}", command.TemplateData.TryGetValue("orderId", out var oid) ? oid : "your order");
                var result = await _mailgun.SendEmailAsync(email!, template.Subject, body, notification.NotificationId.Value, cancellationToken);

                if (result.IsSuccess)
                    notification.MarkAsSent(result.MessageId, command.CorrelationId);
                else
                    notification.MarkAsFailed(result.ErrorMessage ?? "Mailgun rejected the message.", command.CorrelationId);
            }
            else
            {
                var body = template.Body.Replace("{orderId}", command.TemplateData.TryGetValue("orderId", out var oid) ? oid : "your order");
                var result = await _twilio.SendSmsAsync(phone!, body, notification.NotificationId.Value, cancellationToken);

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

    private static NotificationTemplate GetTemplate(string channel, string notificationType)
    {
        var templates = new Dictionary<string, Dictionary<string, NotificationTemplate>>
        {
            [NotificationChannels.Email] = new()
            {
                ["OrderConfirmed"] = new("Your order has been confirmed", "Your order {orderId} has been confirmed and is being processed."),
                ["ShipmentDispatched"] = new("Your order is on its way", "Your order {orderId} has been dispatched."),
                ["DeliveryConfirmed"] = new("Your order has been delivered", "Your order {orderId} has been delivered."),
                ["OrderFailed"] = new("There was a problem with your order", "Unfortunately, your order {orderId} could not be completed."),
            },
            [NotificationChannels.Sms] = new()
            {
                ["OrderConfirmed"] = new("", "Order {orderId} confirmed."),
                ["ShipmentDispatched"] = new("", "Order {orderId} dispatched."),
                ["DeliveryConfirmed"] = new("", "Order {orderId} delivered."),
                ["OrderFailed"] = new("", "Order {orderId} failed."),
            }
        };

        if (templates.TryGetValue(channel, out var channelTemplates) && channelTemplates.TryGetValue(notificationType, out var template))
            return template;

        return channel == NotificationChannels.Email
            ? new("Order update", "There is an update on your order {orderId}.")
            : new("", "Update on order {orderId}.");
    }
}
