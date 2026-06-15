namespace ECommerceOrderProcessing.Infrastructure.Snapshots;

public sealed class AggregateSnapshot
{
    public long SnapshotId { get; init; }
    public Guid AggregateId { get; init; }
    public string AggregateType { get; init; } = string.Empty;
    public int Version { get; init; }
    public string StateJson { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
}
