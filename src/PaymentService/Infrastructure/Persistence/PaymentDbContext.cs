using ECommerceOrderProcessing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace PaymentService.Infrastructure.Persistence;

public sealed class PaymentDbContext : DbContextBase
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<PaymentReadModel> PaymentViewModels => Set<PaymentReadModel>();
    public DbSet<ProcessedWebhook> ProcessedWebhooks => Set<ProcessedWebhook>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PaymentReadModel>(e =>
        {
            e.ToTable("payment_view_models");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(50);
            e.Property(x => x.Amount).HasColumnName("amount").HasColumnType("decimal(18,4)");
            e.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(10);
            e.Property(x => x.StripeChargeId).HasColumnName("stripe_charge_id").HasMaxLength(100);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.StripeChargeId);
        });

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
