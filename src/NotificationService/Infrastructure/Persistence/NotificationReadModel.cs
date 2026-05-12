namespace NotificationService.Infrastructure.Persistence;

public sealed class NotificationReadModel
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
    public string NotificationType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string RecipientAddress { get; set; } = string.Empty;
    public string? ProviderMessageId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
}
