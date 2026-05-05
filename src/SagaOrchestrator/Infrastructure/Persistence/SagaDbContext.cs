using ECommerceOrderProcessing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace SagaOrchestrator.Infrastructure.Persistence;

public sealed class SagaDbContext : DbContextBase
{
    public SagaDbContext(DbContextOptions<SagaDbContext> options) : base(options) { }

    public DbSet<SagaState> SagaStates => Set<SagaState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SagaState>(e =>
        {
            e.ToTable("saga_states");
            e.HasKey(x => x.SagaId);
            e.Property(x => x.SagaId).HasColumnName("saga_id").ValueGeneratedNever();
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(50);
            e.Property(x => x.CurrentStep).HasColumnName("current_step").HasMaxLength(100);
            e.Property(x => x.TotalAmount).HasColumnName("total_amount").HasColumnType("decimal(18,4)");
            e.Property(x => x.Currency).HasColumnName("currency").HasMaxLength(10);
            e.Property(x => x.ShippingAddressJson).HasColumnName("shipping_address").HasColumnType("jsonb");
            e.Property(x => x.ItemsJson).HasColumnName("items").HasColumnType("jsonb");
            e.Property(x => x.PaymentId).HasColumnName("payment_id");
            e.Property(x => x.PaymentAmount).HasColumnName("payment_amount").HasColumnType("decimal(18,4)");
            e.Property(x => x.PaymentCurrency).HasColumnName("payment_currency").HasMaxLength(10);
            e.Property(x => x.ReservationId).HasColumnName("reservation_id");
            e.Property(x => x.CompensationReason).HasColumnName("compensation_reason");
            e.Property(x => x.Version).HasColumnName("version");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            e.HasIndex(x => x.OrderId).IsUnique().HasDatabaseName("ix_saga_states_order_id");
        });
    }
}
