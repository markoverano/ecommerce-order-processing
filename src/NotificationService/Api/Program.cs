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
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using NotificationService.Api.Hubs;
using NotificationService.Api.Notifications;
using NotificationService.Application.Commands;
using NotificationService.Application.ExternalClients;
using NotificationService.Application.Notifications;
using NotificationService.Application.Repositories;
using NotificationService.Application.Validation;
using NotificationService.Application.Webhooks;
using NotificationService.Domain.Repositories;
using NotificationService.Infrastructure.ExternalClients;
using NotificationService.Infrastructure.Messaging;
using NotificationService.Infrastructure.Persistence;
using NotificationService.Infrastructure.Repositories;
using ECommerceOrderProcessing.Infrastructure.Webhooks;
using ECommerceOrderProcessing.Shared.Webhooks;
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
           .Enrich.WithProperty("ServiceName", "NotificationService")
           .Enrich.WithOpenTelemetryTraceId()
           .Enrich.WithOpenTelemetrySpanId()
           .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{ServiceName}] {TraceId} {Message:lj}{NewLine}{Exception}");

        var elasticUri = ctx.Configuration["Elasticsearch__Uri"];
        if (!string.IsNullOrEmpty(elasticUri))
        {
            cfg.WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri(elasticUri))
            {
                AutoRegisterTemplate = true,
                IndexFormat = "ecommerce-notification-{0:yyyy.MM.dd}",
                BatchAction = ElasticOpType.Create,
                FailureCallback = (_, ex) =>
                    Console.Error.WriteLine($"Elasticsearch sink failed: {ex?.Message}")
            });
        }
    });

    var config = builder.Configuration;

    builder.Services.AddDbContext<NotificationDbContext>(opts =>
        opts.UseNpgsql(config.GetConnectionString("NotificationsDb") ?? config["DB_CONNECTION_STRING"]));

    builder.Services.AddScoped<DbContextBase>(sp => sp.GetRequiredService<NotificationDbContext>());
    builder.Services.AddScoped<IEventStore, EfCoreEventStore>();
    builder.Services.AddScoped<IOutboxStore, EfCoreOutboxStore>();
    builder.Services.AddScoped<INotificationRepository, EfCoreNotificationRepository>();
    builder.Services.AddScoped<INotificationReadRepository, EfCoreNotificationReadRepository>();
    builder.Services.AddScoped<IWebhookDeduplicator, EfCoreWebhookDeduplicator<NotificationDbContext>>();
    builder.Services.AddScoped<NotifyCustomerCommandValidator>();
    builder.Services.AddScoped<MailgunWebhookHandler>();
    builder.Services.AddScoped<TwilioWebhookHandler>();
    builder.Services.AddJwtAuthentication(config);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
    builder.Services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();

    var policyRegistry = new PolicyRegistry();
    PollyPolicies.RegisterPolicies(policyRegistry, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, "notification-service");
    builder.Services.AddSingleton<IReadOnlyPolicyRegistry<string>>(policyRegistry);
    builder.Services.AddSingleton<IPolicyRegistry<string>>(policyRegistry);

    builder.Services.AddHttpClient<IMailgunNotificationClient, MailgunNotificationClient>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    builder.Services.AddHttpClient<ITwilioNotificationClient, TwilioNotificationClient>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(30);
    });

    builder.Services.AddMediatR(cfg =>
        cfg.RegisterServicesFromAssemblyContaining<NotifyCustomerCommandHandler>());

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
        return factory.CreateConnection("notification-service");
    });

    builder.Services.AddSingleton<RabbitMqPublisher>();
    builder.Services.AddSingleton<IEventPublisher>(sp => sp.GetRequiredService<RabbitMqPublisher>());
    builder.Services.AddSingleton<IOutboxEventPublisher>(sp => sp.GetRequiredService<RabbitMqPublisher>());
    builder.Services.AddHostedService<OutboxPublisher>();
    builder.Services.AddHostedService<NotificationCommandConsumer>();

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracing => tracing
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("notification-service"))
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddEntityFrameworkCoreInstrumentation()
            .AddSource(MessageConsumerBase.MessagingActivitySource.Name)
            .AddOtlpExporter(opts =>
                opts.Endpoint = new Uri(config["Jaeger__Endpoint"] ?? "http://localhost:4317")));

    builder.Services.AddHealthChecks()
        .AddNpgSql(config.GetConnectionString("NotificationsDb") ?? config["DB_CONNECTION_STRING"] ?? string.Empty, name: "postgres");

    var redisConnection = config["Redis__ConnectionString"] ?? "localhost:6379";
    builder.Services.AddSignalR()
        .AddStackExchangeRedis(redisConnection, opts =>
        {
            opts.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("notification-signalr");
        });
    builder.Services.AddScoped<IOrderStatusNotifier, OrderStatusNotifier>();

    builder.Services.AddCors(opts =>
        opts.AddPolicy("SignalRDev", policy =>
            policy.WithOrigins(config["SignalR__AllowedOrigins"] ?? "http://localhost:3000")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()));

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(opts =>
    {
        opts.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Notification Service",
            Version = "v1",
            Description = "Sends transactional email (Mailgun) and SMS (Twilio) notifications. Receives delivery webhooks from both providers."
        });

        var xmlPath = Path.Combine(AppContext.BaseDirectory, "NotificationService.Api.xml");
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
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        await db.Database.MigrateAsync();
    }

    app.UseSerilogRequestLogging();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseMiddleware<LoggingMiddleware>();

    app.UseRouting();
    app.UseCors("SignalRDev");
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseHttpMetrics();

    app.MapControllers();
    app.MapHub<OrderStatusHub>("/hubs/order-status").RequireCors("SignalRDev");
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
    Log.Fatal(ex, "NotificationService terminated unexpectedly");
}
finally
{
    await Log.CloseAndFlushAsync();
}

public partial class Program;
