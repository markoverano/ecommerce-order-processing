using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ShippingService.Infrastructure.Persistence;

internal sealed class DesignTimeShippingDbContextFactory : IDesignTimeDbContextFactory<ShippingDbContext>
{
    public ShippingDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ShippingDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=shipping_db;Username=ecommerce;Password=changeme")
            .Options;
        return new ShippingDbContext(options);
    }
}
