using ECommerceOrderProcessing.Infrastructure.OutboxStore;
using Microsoft.EntityFrameworkCore;

namespace ECommerceOrderProcessing.Infrastructure.Persistence;

/// <summary>
/// Shared EF Core base that every service DbContext inherits from.
/// Provides the events, outbox, and processed_webhooks tables required by the event-sourcing,
/// outbox, and webhook-deduplication patterns.
/// </summary>
public abstract class DbContextBase : DbContext
{
    protected DbContextBase(DbContextOptions options) : base(options) { }

    public DbSet<StoredEvent> Events => Set<StoredEvent>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedWebhook> ProcessedWebhooks => Set<ProcessedWebhook>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ConfigureEvents(modelBuilder);
        ConfigureOutbox(modelBuilder);
        ConfigureProcessedWebhooks(modelBuilder);
    }

    private static void ConfigureEvents(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<StoredEvent>(e =>
        {
            e.ToTable("events");
            e.HasKey(x => x.EventId);
            e.Property(x => x.EventId).HasColumnName("event_id").UseIdentityAlwaysColumn();
            e.Property(x => x.AggregateId).HasColumnName("aggregate_id");
            e.Property(x => x.AggregateType).HasColumnName("aggregate_type").HasMaxLength(200);
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(200);
            e.Property(x => x.EventData).HasColumnName("event_data").HasColumnType("jsonb");
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.Timestamp).HasColumnName("timestamp");
            e.Property(x => x.CorrelationId).HasColumnName("correlation_id");
            e.HasIndex(x => new { x.AggregateId, x.Version }).IsUnique();
        });
    }

    private static void ConfigureOutbox(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(e =>
        {
            e.ToTable("outbox");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(200);
            e.Property(x => x.EventData).HasColumnName("event_data").HasColumnType("jsonb");
            e.Property(x => x.RoutingKey).HasColumnName("routing_key").HasMaxLength(200);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.PublishedAt).HasColumnName("published_at");
        });
    }

    private static void ConfigureProcessedWebhooks(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProcessedWebhook>(e =>
        {
            e.ToTable("processed_webhooks");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
            e.Property(x => x.WebhookId).HasColumnName("webhook_id").HasMaxLength(200);
            e.Property(x => x.EventType).HasColumnName("event_type").HasMaxLength(100);
            e.Property(x => x.ProcessedAt).HasColumnName("processed_at");
            e.HasIndex(x => x.WebhookId).IsUnique();
        });
    }
}
