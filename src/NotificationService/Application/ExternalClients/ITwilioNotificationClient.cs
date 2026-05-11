namespace NotificationService.Application.ExternalClients;

public interface ITwilioNotificationClient
{
    Task<TwilioSendResult> SendSmsAsync(
        string toPhoneNumber,
        string message,
        Guid notificationId,
        CancellationToken cancellationToken = default);
}

public sealed record TwilioSendResult(bool IsSuccess, string? MessageSid, string? ErrorMessage);
