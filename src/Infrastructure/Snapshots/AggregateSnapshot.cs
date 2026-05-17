namespace ECommerceOrderProcessing.Infrastructure.Snapshots;

public sealed class AggregateSnapshot
{
    public long SnapshotId { get; init; }
    public Guid AggregateId { get; init; }
    public string AggregateType { get; init; } = string.Empty;
    public string SnapshotData { get; init; } = string.Empty;
    public int Version { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
