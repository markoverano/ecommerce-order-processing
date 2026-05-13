using InventoryService.Api.Controllers;
using InventoryService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationService.Api.Controllers;
using OrderService.Api.Controllers;
using PaymentService.Api.Controllers;
using SagaOrchestrator.Api.Controllers;
using ShippingService.Api.Controllers;

namespace E2ETests;

// ──────────────────────────────────────────────────────────────────────────────
// Each factory overrides infrastructure config to point at test containers.
// All external API base URLs are redirected to WireMock stubs so no network calls
// leave the test process. Background services (consumers, outbox) run in-process
// and connect to the same shared PostgreSQL + RabbitMQ TestContainers instances.
// ──────────────────────────────────────────────────────────────────────────────

internal sealed class OrderServiceFactory : WebApplicationFactory<OrdersController>
{
    private readonly FactoryConfig _cfg;
    public OrderServiceFactory(FactoryConfig cfg) => _cfg = cfg;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:OrdersDb"] = _cfg.OrdersConnStr,
            ["RabbitMQ__Host"] = _cfg.RabbitHost,
            ["RabbitMQ__Port"] = _cfg.RabbitPort.ToString(),
            ["RabbitMQ__Username"] = _cfg.RabbitUser,
            ["RabbitMQ__Password"] = _cfg.RabbitPass,
            ["Jaeger__Host"] = "localhost",
            ["Serilog:MinimumLevel:Default"] = "Warning",
        }));
    }
}

internal sealed class PaymentServiceFactory : WebApplicationFactory<PaymentsController>
{
    private readonly FactoryConfig _cfg;
    public PaymentServiceFactory(FactoryConfig cfg) => _cfg = cfg;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:PaymentsDb"] = _cfg.PaymentsConnStr,
            ["RabbitMQ__Host"] = _cfg.RabbitHost,
            ["RabbitMQ__Port"] = _cfg.RabbitPort.ToString(),
            ["RabbitMQ__Username"] = _cfg.RabbitUser,
            ["RabbitMQ__Password"] = _cfg.RabbitPass,
            ["Stripe__ApiKey"] = "sk_test_stub",
            ["Stripe__BaseUrl"] = _cfg.StripeBaseUrl,
            ["Stripe__WebhookSecret"] = "whsec_test_stub",
            ["Jaeger__Host"] = "localhost",
            ["Serilog:MinimumLevel:Default"] = "Warning",
        }));
    }
}

internal sealed class InventoryServiceFactory : WebApplicationFactory<InventoryController>
{
    private readonly FactoryConfig _cfg;
    public InventoryServiceFactory(FactoryConfig cfg) => _cfg = cfg;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:InventoryDb"] = _cfg.InventoryConnStr,
            ["RabbitMQ__Host"] = _cfg.RabbitHost,
            ["RabbitMQ__Port"] = _cfg.RabbitPort.ToString(),
            ["RabbitMQ__Username"] = _cfg.RabbitUser,
            ["RabbitMQ__Password"] = _cfg.RabbitPass,
            ["Jaeger__Host"] = "localhost",
            ["Serilog:MinimumLevel:Default"] = "Warning",
        }));
    }
}

internal sealed class ShippingServiceFactory : WebApplicationFactory<ShipmentsController>
{
    private readonly FactoryConfig _cfg;
    public ShippingServiceFactory(FactoryConfig cfg) => _cfg = cfg;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ShippingDb"] = _cfg.ShippingConnStr,
            ["RabbitMQ__Host"] = _cfg.RabbitHost,
            ["RabbitMQ__Port"] = _cfg.RabbitPort.ToString(),
            ["RabbitMQ__Username"] = _cfg.RabbitUser,
            ["RabbitMQ__Password"] = _cfg.RabbitPass,
            ["FedEx__ApiKey"] = "fedex-test-key",
            ["FedEx__BaseUrl"] = _cfg.FedExBaseUrl,
            ["Jaeger__Host"] = "localhost",
            ["Serilog:MinimumLevel:Default"] = "Warning",
        }));
    }
}

internal sealed class NotificationServiceFactory : WebApplicationFactory<NotificationsController>
{
    private readonly FactoryConfig _cfg;
    public NotificationServiceFactory(FactoryConfig cfg) => _cfg = cfg;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:NotificationsDb"] = _cfg.NotificationsConnStr,
            ["RabbitMQ__Host"] = _cfg.RabbitHost,
            ["RabbitMQ__Port"] = _cfg.RabbitPort.ToString(),
            ["RabbitMQ__Username"] = _cfg.RabbitUser,
            ["RabbitMQ__Password"] = _cfg.RabbitPass,
            ["Mailgun__ApiKey"] = "mailgun-test-key",
            ["Mailgun__Domain"] = "test.mailgun.org",
            ["Mailgun__BaseUrl"] = _cfg.MailgunBaseUrl,
            ["Twilio__AccountSid"] = "AC_test_stub",
            ["Twilio__AuthToken"] = "twilio_test_token",
            ["Jaeger__Host"] = "localhost",
            ["Serilog:MinimumLevel:Default"] = "Warning",
        }));
    }
}

internal sealed class SagaOrchestratorFactory : WebApplicationFactory<SagasController>
{
    private readonly FactoryConfig _cfg;
    public SagaOrchestratorFactory(FactoryConfig cfg) => _cfg = cfg;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:SagaDb"] = _cfg.SagaConnStr,
            ["RabbitMQ__Host"] = _cfg.RabbitHost,
            ["RabbitMQ__Port"] = _cfg.RabbitPort.ToString(),
            ["RabbitMQ__Username"] = _cfg.RabbitUser,
            ["RabbitMQ__Password"] = _cfg.RabbitPass,
            ["Jaeger__Host"] = "localhost",
            ["Serilog:MinimumLevel:Default"] = "Warning",
        }));
    }
}

/// <summary>Shared configuration passed to all service factories.</summary>
internal sealed record FactoryConfig(
    string OrdersConnStr,
    string PaymentsConnStr,
    string InventoryConnStr,
    string ShippingConnStr,
    string NotificationsConnStr,
    string SagaConnStr,
    string RabbitHost,
    int RabbitPort,
    string RabbitUser,
    string RabbitPass,
    string StripeBaseUrl,
    string FedExBaseUrl,
    string MailgunBaseUrl);

/// <summary>Helpers to seed product data into the inventory service's read model for test setup.</summary>
internal static class InventorySeeder
{
    public static async Task SeedProductAsync(
        InventoryServiceFactory factory,
        Guid productId,
        string name,
        int availableQuantity,
        CancellationToken cancellationToken = default)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        db.Products.Add(new ProductReadModel
        {
            Id = productId,
            Name = name,
            AvailableQuantity = availableQuantity,
            ReservedQuantity = 0,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(cancellationToken);
    }
}
