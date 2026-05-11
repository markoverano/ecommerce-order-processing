namespace NotificationService.Api.Requests;

public sealed record SendNotificationRequest(
    Guid OrderId,
    Guid CustomerId,
    string NotificationType,
    Dictionary<string, string> TemplateData);
