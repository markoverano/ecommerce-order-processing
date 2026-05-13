using Npgsql;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace E2ETests;

/// <summary>
/// xUnit collection fixture that starts shared infrastructure (PostgreSQL, RabbitMQ) and all six
/// service factories exactly once per test run, then disposes everything after the last test completes.
/// Tests in the [Collection("E2E")] xUnit collection share this fixture.
/// </summary>
public sealed class ServiceFixture : IAsyncLifetime
{
    private static readonly string[] ServiceDatabases =
    [
        "orders_db", "payments_db", "inventory_db", "shipping_db", "notifications_db", "saga_db"
    ];

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .WithDatabase("postgres")
        .WithUsername("ecommerce")
        .WithPassword("test_password")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder()
        .WithImage("rabbitmq:3.13-management")
        .WithUsername("guest")
        .WithPassword("guest")
        .Build();

    public readonly StripeStubServer Stripe = new();
    public readonly FedExStubServer FedEx = new();
    public readonly MailgunStubServer Mailgun = new();

    private OrderServiceFactory? _orderFactory;
    private PaymentServiceFactory? _paymentFactory;
    private InventoryServiceFactory? _inventoryFactory;
    private ShippingServiceFactory? _shippingFactory;
    private NotificationServiceFactory? _notificationFactory;
    private SagaOrchestratorFactory? _sagaFactory;

    public HttpClient OrderClient { get; private set; } = null!;
    public HttpClient PaymentClient { get; private set; } = null!;
    public HttpClient InventoryClient { get; private set; } = null!;
    public HttpClient ShippingClient { get; private set; } = null!;
    public HttpClient NotificationClient { get; private set; } = null!;
    public HttpClient SagaClient { get; private set; } = null!;

    internal InventoryServiceFactory InventoryFactory => _inventoryFactory!;
    internal PaymentServiceFactory PaymentFactory => _paymentFactory!;

    // Stable product ID shared across all tests for convenience; tests use unique order IDs.
    public static readonly Guid StockProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid OutOfStockProductId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    public async Task InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        await CreateServiceDatabasesAsync();
        await SetupRabbitMqTopologyAsync();

        Stripe.SetupChargeSuccess();
        FedEx.SetupShipmentSuccess();
        Mailgun.SetupEmailSuccess();

        var cfg = BuildFactoryConfig();
        _orderFactory = new OrderServiceFactory(cfg);
        _paymentFactory = new PaymentServiceFactory(cfg);
        _inventoryFactory = new InventoryServiceFactory(cfg);
        _shippingFactory = new ShippingServiceFactory(cfg);
        _notificationFactory = new NotificationServiceFactory(cfg);
        _sagaFactory = new SagaOrchestratorFactory(cfg);

        // Creating a client starts the service's hosted services (consumers, outbox publisher).
        OrderClient = _orderFactory.CreateClient();
        PaymentClient = _paymentFactory.CreateClient();
        InventoryClient = _inventoryFactory.CreateClient();
        ShippingClient = _shippingFactory.CreateClient();
        NotificationClient = _notificationFactory.CreateClient();
        SagaClient = _sagaFactory.CreateClient();

        await SeedInventoryAsync();
    }

    public async Task DisposeAsync()
    {
        OrderClient.Dispose();
        PaymentClient.Dispose();
        InventoryClient.Dispose();
        ShippingClient.Dispose();
        NotificationClient.Dispose();
        SagaClient.Dispose();

        if (_orderFactory is not null) await _orderFactory.DisposeAsync();
        if (_paymentFactory is not null) await _paymentFactory.DisposeAsync();
        if (_inventoryFactory is not null) await _inventoryFactory.DisposeAsync();
        if (_shippingFactory is not null) await _shippingFactory.DisposeAsync();
        if (_notificationFactory is not null) await _notificationFactory.DisposeAsync();
        if (_sagaFactory is not null) await _sagaFactory.DisposeAsync();

        Stripe.Dispose();
        FedEx.Dispose();
        Mailgun.Dispose();

        await _postgres.StopAsync();
        await _rabbitMq.StopAsync();
    }

    private async Task CreateServiceDatabasesAsync()
    {
        await using var conn = new NpgsqlConnection(_postgres.GetConnectionString());
        await conn.OpenAsync();

        foreach (var db in ServiceDatabases)
        {
            await using var cmd = conn.CreateCommand();
            // IF NOT EXISTS is not supported for CREATE DATABASE; catch and ignore duplicate errors.
            cmd.CommandText = $"CREATE DATABASE {db}";
            try { await cmd.ExecuteNonQueryAsync(); }
            catch (PostgresException ex) when (ex.SqlState == "42P04") { /* already exists */ }
        }
    }

    private async Task SetupRabbitMqTopologyAsync()
    {
        // Wait for RabbitMQ to be ready, then declare the exchange and all service queues.
        // The services also declare their own queues on startup, but pre-declaring ensures
        // there are no race conditions during the first test run.
        await WaitHelper.WaitForConditionAsync(
            () =>
            {
                try
                {
                    var factory = new ConnectionFactory
                    {
                        HostName = _rabbitMq.Hostname,
                        Port = _rabbitMq.GetMappedPublicPort(5672),
                        UserName = "guest",
                        Password = "guest"
                    };
                    using var conn = factory.CreateConnection("topology-setup");
                    using var channel = conn.CreateModel();
                    channel.ExchangeDeclare("order.events", ExchangeType.Topic, durable: true, autoDelete: false);

                    foreach (var (queue, routingKey) in GetQueueBindings())
                    {
                        channel.QueueDeclare(queue, durable: true, exclusive: false, autoDelete: false);
                        channel.QueueBind(queue, "order.events", routingKey);
                    }
                    return Task.FromResult(true);
                }
                catch { return Task.FromResult(false); }
            },
            timeout: TimeSpan.FromSeconds(30),
            description: "RabbitMQ topology setup");
    }

    private static IEnumerable<(string Queue, string RoutingKey)> GetQueueBindings()
    {
        yield return ("saga-orchestrator.events", "order.created");
        yield return ("saga-orchestrator.events", "payment.processed");
        yield return ("saga-orchestrator.events", "payment.failed");
        yield return ("saga-orchestrator.events", "payment.refunded");
        yield return ("saga-orchestrator.events", "stock.reserved");
        yield return ("saga-orchestrator.events", "stock.out");
        yield return ("saga-orchestrator.events", "shipment.created");
        yield return ("saga-orchestrator.events", "shipment.failed");
        yield return ("saga-orchestrator.events", "notification.sent");
        yield return ("payment-service.commands", "command.process-payment");
        yield return ("payment-service.commands", "command.refund-payment");
        yield return ("inventory-service.commands", "command.reserve-stock");
        yield return ("inventory-service.commands", "command.release-stock");
        yield return ("shipping-service.commands", "command.create-shipment");
        yield return ("shipping-service.commands", "command.cancel-shipment");
        yield return ("notification-service.commands", "command.notify-customer");
        yield return ("order-service.events", "order.confirmed");
        yield return ("order-service.events", "order.failed");
        yield return ("order-service.events", "order.compensated");
    }

    private async Task SeedInventoryAsync()
    {
        await InventorySeeder.SeedProductAsync(_inventoryFactory!, StockProductId, "Test Widget", availableQuantity: 100);
        await InventorySeeder.SeedProductAsync(_inventoryFactory!, OutOfStockProductId, "Scarce Item", availableQuantity: 0);
    }

    private FactoryConfig BuildFactoryConfig()
    {
        var baseConnStr = _postgres.GetConnectionString();

        static string WithDb(string connStr, string dbName)
        {
            var builder = new NpgsqlConnectionStringBuilder(connStr) { Database = dbName };
            return builder.ToString();
        }

        return new FactoryConfig(
            OrdersConnStr: WithDb(baseConnStr, "orders_db"),
            PaymentsConnStr: WithDb(baseConnStr, "payments_db"),
            InventoryConnStr: WithDb(baseConnStr, "inventory_db"),
            ShippingConnStr: WithDb(baseConnStr, "shipping_db"),
            NotificationsConnStr: WithDb(baseConnStr, "notifications_db"),
            SagaConnStr: WithDb(baseConnStr, "saga_db"),
            RabbitHost: _rabbitMq.Hostname,
            RabbitPort: _rabbitMq.GetMappedPublicPort(5672),
            RabbitUser: "guest",
            RabbitPass: "guest",
            StripeBaseUrl: Stripe.BaseUrl,
            FedExBaseUrl: FedEx.BaseUrl,
            MailgunBaseUrl: Mailgun.BaseUrl);
    }
}

[CollectionDefinition("E2E")]
public sealed class E2ECollection : ICollectionFixture<ServiceFixture> { }
