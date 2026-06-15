using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrderService.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Testcontainers.Redis;
using Xunit;

namespace OrderService.Integration.Tests.Infrastructure;

/// <summary>
/// Spins up real PostgreSQL, RabbitMQ, and Redis containers for the duration of the test class.
/// Inherits from <see cref="WebApplicationFactory{TEntryPoint}"/> to provide a real HTTP test server.
/// </summary>
public sealed class OrderServiceFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithDatabase("orders_db")
        .WithUsername("ecommerce")
        .WithPassword("testpass")
        .Build();

    private readonly RabbitMqContainer _rabbit = new RabbitMqBuilder()
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    private readonly RedisContainer _redis = new RedisBuilder().Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _postgres.StartAsync(),
            _rabbit.StartAsync(),
            _redis.StartAsync());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Test");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<OrderDbContext>>();
            services.AddDbContext<OrderDbContext>(opts =>
                opts.UseNpgsql(_postgres.GetConnectionString()));

            services.RemoveAll<IDistributedCache>();
            services.AddStackExchangeRedisCache(opts =>
                opts.Configuration = _redis.GetConnectionString());
        });
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(
            _postgres.DisposeAsync().AsTask(),
            _rabbit.DisposeAsync().AsTask(),
            _redis.DisposeAsync().AsTask());
    }
}
