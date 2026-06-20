namespace AnalyticsService.Domain.Entities;

public record NotificationMetric
{
    public long Id { get; init; }
    public required Guid NotificationId { get; init; }
    public required string Type { get; set; }
    public required string Status { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? FailedAt { get; set; }

    public static NotificationMetric Create(Guid notificationId, string type, string status)
    {
        return new NotificationMetric
        {
            NotificationId = notificationId,
            Type = type,
            Status = status,
            SentAt = DateTime.UtcNow
        };
    }
}
