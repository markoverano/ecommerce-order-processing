namespace NotificationService.Application.DTOs;

public sealed record NotificationDto(
    Guid Id,
    Guid OrderId,
    Guid CustomerId,
    string NotificationType,
    string Status,
    string Channel,
    string RecipientAddress,
    string? ProviderMessageId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
