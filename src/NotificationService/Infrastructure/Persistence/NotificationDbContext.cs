using ECommerceOrderProcessing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace NotificationService.Infrastructure.Persistence;

public sealed class NotificationDbContext : DbContextBase
{
    public NotificationDbContext(DbContextOptions<NotificationDbContext> options) : base(options) { }

    public DbSet<NotificationReadModel> NotificationViewModels => Set<NotificationReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NotificationReadModel>(e =>
        {
            e.ToTable("notification_view_models");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.NotificationType).HasColumnName("notification_type").HasMaxLength(100);
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(50);
            e.Property(x => x.Channel).HasColumnName("channel").HasMaxLength(20);
            e.Property(x => x.RecipientAddress).HasColumnName("recipient_address").HasMaxLength(500);
            e.Property(x => x.ProviderMessageId).HasColumnName("provider_message_id").HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.OrderId);
            e.HasIndex(x => x.ProviderMessageId);
        });
    }
}
