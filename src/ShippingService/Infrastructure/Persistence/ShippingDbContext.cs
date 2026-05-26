using ECommerceOrderProcessing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ShippingService.Infrastructure.Persistence;

public sealed class ShippingDbContext : DbContextBase
{
    public ShippingDbContext(DbContextOptions<ShippingDbContext> options) : base(options) { }

    public DbSet<ShipmentReadModel> ShipmentViewModels => Set<ShipmentReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ShipmentReadModel>(e =>
        {
            e.ToTable("shipment_view_models");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.CustomerId).HasColumnName("customer_id");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(50);
            e.Property(x => x.TrackingNumber).HasColumnName("tracking_number").HasMaxLength(100);
            e.Property(x => x.DestinationAddress).HasColumnName("destination_address").HasMaxLength(500);
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.TrackingNumber);
            e.HasIndex(x => x.OrderId);
        });
    }
}
