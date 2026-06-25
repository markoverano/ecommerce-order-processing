using AnalyticsService.Domain.Entities;
using ECommerceOrderProcessing.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AnalyticsService.Infrastructure.Persistence;

public class AnalyticsDbContext : DbContextBase
{
    public DbSet<SalesSummary> SalesSummaries => Set<SalesSummary>();
    public DbSet<OrderMetric> OrderMetrics => Set<OrderMetric>();
    public DbSet<PaymentMetric> PaymentMetrics => Set<PaymentMetric>();
    public DbSet<InventoryMetric> InventoryMetrics => Set<InventoryMetric>();
    public DbSet<ShippingMetric> ShippingMetrics => Set<ShippingMetric>();
    public DbSet<CustomerMetric> CustomerMetrics => Set<CustomerMetric>();
    public DbSet<NotificationMetric> NotificationMetrics => Set<NotificationMetric>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SalesSummary>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Date).HasConversion<long>();
            entity.HasIndex(e => e.Date).IsUnique();
            entity.Property(e => e.TotalRevenue).HasPrecision(15, 2);
            entity.Property(e => e.AverageOrderValue).HasPrecision(10, 2);
        });

        modelBuilder.Entity<OrderMetric>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.OrderId).IsUnique();
            entity.HasIndex(e => e.CustomerId);
            entity.HasIndex(e => e.CreatedAt);
            entity.Property(e => e.OrderValue).HasPrecision(10, 2);
        });

        modelBuilder.Entity<PaymentMetric>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.PaymentId).IsUnique();
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.Status);
            entity.Property(e => e.Amount).HasPrecision(10, 2);
        });

        modelBuilder.Entity<InventoryMetric>(entity =>
        {
            entity.HasKey(e => e.Id);
            // Composite unique: one record per product per reservation
            entity.HasIndex(e => new { e.ReservationId, e.ProductId }).IsUnique();
            entity.HasIndex(e => e.ProductId);
        });

        modelBuilder.Entity<ShippingMetric>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ShipmentId).IsUnique();
            entity.HasIndex(e => e.CreatedAt);
        });

        modelBuilder.Entity<CustomerMetric>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CustomerId).IsUnique();
            entity.Property(e => e.LifetimeValue).HasPrecision(15, 2);
            entity.Property(e => e.AverageOrderValue).HasPrecision(10, 2);
            entity.Property(e => e.RepeatRate).HasPrecision(5, 2);
        });

        modelBuilder.Entity<NotificationMetric>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.NotificationId).IsUnique();
            entity.HasIndex(e => e.Type);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<ProcessedEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EventId).IsUnique();
            entity.HasIndex(e => e.ProcessedAt);
        });
    }
}
