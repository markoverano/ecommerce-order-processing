using ECommerceOrderProcessing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Infrastructure.Persistence;

public sealed class InventoryDbContext : DbContextBase
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options) : base(options) { }

    public DbSet<StockReservationReadModel> StockReservations => Set<StockReservationReadModel>();
    public DbSet<ProductReadModel> Products => Set<ProductReadModel>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<StockReservationReadModel>(e =>
        {
            e.ToTable("stock_reservations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.OrderId).HasColumnName("order_id");
            e.Property(x => x.Status).HasColumnName("status").HasMaxLength(50);
            e.Property(x => x.ItemsJson).HasColumnName("items_json").HasColumnType("jsonb");
            e.Property(x => x.ExpiresAt).HasColumnName("expires_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<ProductReadModel>(e =>
        {
            e.ToTable("products");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            e.Property(x => x.Name).HasColumnName("name").HasMaxLength(255);
            e.Property(x => x.AvailableQuantity).HasColumnName("available_quantity");
            e.Property(x => x.ReservedQuantity).HasColumnName("reserved_quantity");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
