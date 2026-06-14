using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SagaOrchestrator.Infrastructure.Persistence;

internal sealed class DesignTimeSagaDbContextFactory : IDesignTimeDbContextFactory<SagaDbContext>
{
    public SagaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SagaDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=saga_db;Username=ecommerce;Password=changeme")
            .Options;
        return new SagaDbContext(options);
    }
}
