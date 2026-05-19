using ECommerceOrderProcessing.Infrastructure.Auth;
using ECommerceOrderProcessing.Infrastructure.EventStore;
using ECommerceOrderProcessing.Shared.Auth;
using Microsoft.OpenApi.Models;
using Serilog.Enrichers.OpenTelemetry;
using ECommerceOrderProcessing.Infrastructure.Messaging;
using ECommerceOrderProcessing.Infrastructure.Messaging.RabbitMq;
using ECommerceOrderProcessing.Infrastructure.Middleware;
using ECommerceOrderProcessing.Infrastructure.OutboxStore;
using ECommerceOrderProcessing.Infrastructure.Persistence;
using ECommerceOrderProcessing.Infrastructure.Resilience;
using InventoryService.Application.Commands;
using InventoryService.Application.Repositories;
using InventoryService.Application.Validation;
using InventoryService.Domain.Repositories;
using InventoryService.Infrastructure.BackgroundServices;
using InventoryService.Infrastructure.Messaging;
using InventoryService.Infrastructure.Persistence;
using InventoryService.Infrastructure.Repositories;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Polly.Registry;
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
           .Enrich.WithProperty("ServiceName", "InventoryService")
           .Enrich.WithOpenTelemetryTraceId()
           .Enrich.WithOpenTelemetrySpanId()
           .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServiceName}] {TraceId} {Message:lj}{NewLine}{Exception}");

        var elasticUri = ctx.Configuration["Elasticsearch__Uri"];
        if (!string.IsNullOrEmpty(elasticUri))
        {
            cfg.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticUri))
            {
                AutoRegisterTemplate = true,
                IndexFormat = "ecommerce-inventory-{0:yyyy.MM.dd}",
                BatchAction = ElasticOpType.Create,
                FailureCallback = (_, ex) =>
                    Console.Error.WriteLine($"Elasticsearch sink failed: {ex?.Message}")
            });
        }
    });

    var config = builder.Configuration;

    builder.Services.AddDbContext<InventoryDbContext>(opts =>
        opts.UseNpgsql(config.GetConnectionString("InventoryDb") ?? config["DB_CONNECTION_STRING"]));

    builder.Services.AddScoped<DbContextBase>(sp => sp.GetRequiredService<InventoryDbContext>());
    builder.Services.AddScoped<IEventStore, EfCoreEventStore>();
    builder.Services.AddScoped<IOutboxStore, EfCoreOutboxStore>();

    builder.Services.AddScoped<IStockReservationRepository, EfCoreStockReservationRepository>();
    builder.Services.AddScoped<IProductRepository, EfCoreProductRepository>();
    builder.Services.AddScoped<IStockReadRepository, EfCoreStockReadRepository>();

    builder.Services.AddScoped<ReserveStockCommandValidator>();
    builder.Services.AddJwtAuthentication(config);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();

    var policyRegistry = new PolicyRegistry();
    PollyPolicies.RegisterPolicies(policyRegistry, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, "inventory-service");
    builder.Services.AddSingleton<IReadOnlyPolicyRegistry<string>>(policyRegistry);
    builder.Services.AddSingleton<IPolicyRegistry<string>>(policyRegistry);

    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssemblyContaining<ReserveStockCommandHandler>());

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
        return factory.CreateConnection("inventory-service");
    });

    builder.Services.AddSingleton<IEventPublisher, RabbitMqPublisher>();
    builder.Services.AddHostedService<OutboxPublisher>();
    builder.Services.AddHostedService<InventoryCommandConsumer>();
    builder.Services.AddHostedService<ReservationExpiryService>();

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("inventory-service"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource(MessageConsumerBase.MessagingActivitySource.Name)
            .AddOtlpExporter(opts =>
                opts.Endpoint = new Uri(config["Jaeger__Endpoint"] ?? "http://localhost:4317")));

    builder.Services.AddHealthChecks()
        .AddNpgSql(config.GetConnectionString("InventoryDb") ?? config["DB_CONNECTION_STRING"] ?? string.Empty, name: "postgres");

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(opts =>
    {
        opts.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Inventory Service",
            Version = "v1",
            Description = "Manages stock reservations with a 2-hour TTL. Consumes ReserveStockCommand and ReleaseStockCommand from RabbitMQ."
        });

        var xmlPath = Path.Combine(AppContext.BaseDirectory, "InventoryService.Api.xml");
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
        var db = scope.ServiceProvider.GetRequiredService<InventoryDbContext>();
        await db.Database.MigrateAsync();
    }

    app.UseSerilogRequestLogging();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseMiddleware<LoggingMiddleware>();

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
    Log.Fatal(ex, "InventoryService terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
