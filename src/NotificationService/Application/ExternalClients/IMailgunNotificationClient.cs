namespace NotificationService.Application.ExternalClients;

public interface IMailgunNotificationClient
{
    Task<MailgunSendResult> SendEmailAsync(
        string toAddress,
        string subject,
        string body,
        Guid notificationId,
        CancellationToken cancellationToken = default);
}

public sealed record MailgunSendResult(bool IsSuccess, string? MessageId, string? ErrorMessage);
