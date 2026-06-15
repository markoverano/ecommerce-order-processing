using ECommerceOrderProcessing.Infrastructure.Auth;
using ECommerceOrderProcessing.Infrastructure.EventStore;
using ECommerceOrderProcessing.Shared.Auth;
using Microsoft.OpenApi.Models;
using Serilog.Enrichers.OpenTelemetry;
using ECommerceOrderProcessing.Infrastructure.Idempotency;
using ECommerceOrderProcessing.Infrastructure.Messaging;
using ECommerceOrderProcessing.Infrastructure.Messaging.RabbitMq;
using ECommerceOrderProcessing.Infrastructure.Middleware;
using ECommerceOrderProcessing.Infrastructure.OutboxStore;
using ECommerceOrderProcessing.Infrastructure.Persistence;
using ECommerceOrderProcessing.Infrastructure.RateLimiting;
using ECommerceOrderProcessing.Infrastructure.Snapshots;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OrderService.Api.Middleware;
using OrderService.Application.Commands;
using OrderService.Application.Repositories;
using OrderService.Application.Validation;
using OrderService.Domain.Repositories;
using OrderService.Infrastructure.Caching;
using OrderService.Infrastructure.Messaging;
using OrderService.Infrastructure.Persistence;
using OrderService.Infrastructure.Repositories;
using Prometheus;
using RabbitMQ.Client;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.Elasticsearch;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}")
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, cfg) =>
    {
        cfg.ReadFrom.Configuration(ctx.Configuration)
           .ReadFrom.Services(services)
           .Enrich.FromLogContext()
           .Enrich.WithProperty("ServiceName", "OrderService")
           .Enrich.WithOpenTelemetryTraceId()
           .Enrich.WithOpenTelemetrySpanId()
           .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServiceName}] {TraceId} {Message:lj}{NewLine}{Exception}");

        var elasticUri = ctx.Configuration["Elasticsearch__Uri"];
        if (!string.IsNullOrEmpty(elasticUri))
        {
            var elasticUser = ctx.Configuration["Elasticsearch__Username"];
            var elasticPass = ctx.Configuration["Elasticsearch__Password"];
            cfg.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticUri))
            {
                AutoRegisterTemplate = true,
                IndexFormat = "ecommerce-order-{0:yyyy.MM.dd}",
                BatchAction = ElasticOpType.Create,
                ModifyConnectionSettings = conn =>
                    !string.IsNullOrEmpty(elasticUser) && !string.IsNullOrEmpty(elasticPass)
                        ? conn.BasicAuthentication(elasticUser, elasticPass)
                        : conn,
                FailureCallback = (_, ex) =>
                    Console.Error.WriteLine($"Elasticsearch sink failed: {ex?.Message}")
            });
        }
    });

    var config = builder.Configuration;

    builder.Services.AddDbContext<OrderDbContext>(opts =>
        opts.UseNpgsql(config.GetConnectionString("OrdersDb") ?? config["DB_CONNECTION_STRING"]));

    builder.Services.AddScoped<DbContextBase>(sp => sp.GetRequiredService<OrderDbContext>());
    builder.Services.AddScoped<IEventStore, EfCoreEventStore>();
    builder.Services.AddScoped<IOutboxStore, EfCoreOutboxStore>();
    builder.Services.AddScoped<IOrderRepository, EfCoreOrderRepository>();
    builder.Services.AddScoped<EfCoreOrderReadRepository>();
    builder.Services.AddScoped<IOrderReadRepository>(sp =>
        new CachedOrderReadRepository(
            sp.GetRequiredService<EfCoreOrderReadRepository>(),
            sp.GetRequiredService<Microsoft.Extensions.Caching.Distributed.IDistributedCache>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CachedOrderReadRepository>>()));
    builder.Services.AddScoped<CreateOrderCommandValidator>();
    builder.Services.AddJwtAuthentication(config);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
    builder.Services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();

    builder.Services.AddStackExchangeRedisCache(opts =>
    {
        opts.Configuration = config["Redis__ConnectionString"] ?? "localhost:6379";
        opts.InstanceName = "order-service:";
    });
    builder.Services.AddScoped<IGlobalIdempotencyStore, RedisGlobalIdempotencyStore>();

    builder.Services.AddScoped<ISnapshotStore, EfCoreSnapshotStore<OrderDbContext>>();

    builder.Services.AddSingleton<IExceptionMapper, DefaultExceptionMapper>();
    builder.Services.Configure<RateLimitingOptions>(config.GetSection(RateLimitingOptions.SectionName));

    builder.Services.AddMediatR(cfg =>
    {
        cfg.RegisterServicesFromAssemblyContaining<CreateOrderCommandHandler>();
        cfg.AddOpenBehavior(typeof(GlobalIdempotencyBehavior<,>));
    });

    builder.Services.AddSingleton<IConnection>(_ =>
    {
        var factory = new ConnectionFactory
        {
            HostName = config["RabbitMQ__Host"] ?? "localhost",
            Port = int.TryParse(config["RabbitMQ__Port"], out var port) ? port : 5672,
            UserName = config["RabbitMQ__Username"] ?? "guest",
            Password = config["RabbitMQ__Password"] ?? "guest",
            DispatchConsumersAsync = true
        };
        return factory.CreateConnection("order-service");
    });

    builder.Services.AddSingleton<RabbitMqPublisher>();
    builder.Services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<RabbitMqPublisher>());
    builder.Services.AddSingleton<IOutboxEventPublisher>(sp => sp.GetRequiredService<RabbitMqPublisher>());
    builder.Services.AddHostedService<OutboxPublisher>();
    builder.Services.AddHostedService<OrderEventConsumer>();

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("order-service"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource(MessageConsumerBase.MessagingActivitySource.Name)
            .AddOtlpExporter(opts =>
                opts.Endpoint = new Uri(config["Jaeger__Endpoint"] ?? "http://localhost:4317")));

    builder.Services.AddHealthChecks()
        .AddNpgSql(config.GetConnectionString("OrdersDb") ?? config["DB_CONNECTION_STRING"] ?? string.Empty, name: "postgres")
        .AddRedis(config["Redis__ConnectionString"] ?? "localhost:6379", name: "redis");

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(opts =>
    {
        opts.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Order Service",
            Version = "v1",
            Description = "Manages order lifecycle. Publishes OrderCreated to trigger the saga orchestrator."
        });

        var xmlPath = Path.Combine(AppContext.BaseDirectory, "OrderService.Api.xml");
        if (File.Exists(xmlPath))
            opts.IncludeXmlComments(xmlPath);

        var oidcAuthority = config["Oidc__Authority"] ?? "http://keycloak:8080/realms/ecommerce";
        opts.AddSecurityDefinition("oidc", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OpenIdConnect,
            OpenIdConnectUrl = new Uri($"{oidcAuthority}/.well-known/openid-configuration")
        });

        opts.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "oidc" }
                },
                new[] { Roles.Customer, Roles.Admin }
            }
        });
    });

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
        await db.Database.MigrateAsync();
    }

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    app.UseSerilogRequestLogging();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseMiddleware<LoggingMiddleware>();
    app.UseMiddleware<RateLimitingMiddleware>();
    app.UseMiddleware<IdempotencyMiddleware>();

    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseHttpMetrics();

    app.MapControllers();
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready");
    app.MapMetrics("/metrics");

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "OrderService terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
