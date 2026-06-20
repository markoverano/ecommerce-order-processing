using AnalyticsService.Domain.Repositories;
using AnalyticsService.Infrastructure.Messaging;
using AnalyticsService.Infrastructure.Persistence;
using AnalyticsService.Infrastructure.Repositories;
using ECommerceOrderProcessing.Infrastructure.Auth;
using ECommerceOrderProcessing.Infrastructure.Messaging;
using ECommerceOrderProcessing.Infrastructure.Messaging.RabbitMq;
using ECommerceOrderProcessing.Infrastructure.Middleware;
using ECommerceOrderProcessing.Infrastructure.Persistence;
using ECommerceOrderProcessing.Shared.Auth;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Prometheus;
using RabbitMQ.Client;
using Serilog;
using Serilog.Events;
using Serilog.Enrichers.OpenTelemetry;
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
           .Enrich.WithProperty("ServiceName", "AnalyticsService")
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
                IndexFormat = "ecommerce-analytics-{0:yyyy.MM.dd}",
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

    builder.Services.AddDbContext<AnalyticsDbContext>(opts =>
        opts.UseNpgsql(config.GetConnectionString("AnalyticsDb") ?? config["DB_CONNECTION_STRING"]));

    builder.Services.AddScoped<DbContextBase>(sp => sp.GetRequiredService<AnalyticsDbContext>());

    builder.Services.AddScoped<ISalesSummaryRepository, EfCoreSalesSummaryRepository>();
    builder.Services.AddScoped<IOrderMetricRepository, EfCoreOrderMetricRepository>();
    builder.Services.AddScoped<IPaymentMetricRepository, EfCorePaymentMetricRepository>();
    builder.Services.AddScoped<IInventoryMetricRepository, EfCoreInventoryMetricRepository>();
    builder.Services.AddScoped<IShippingMetricRepository, EfCoreShippingMetricRepository>();
    builder.Services.AddScoped<ICustomerMetricRepository, EfCoreCustomerMetricRepository>();
    builder.Services.AddScoped<INotificationMetricRepository, EfCoreNotificationMetricRepository>();
    builder.Services.AddScoped<IProcessedEventRepository, EfCoreProcessedEventRepository>();

    builder.Services.AddSingleton<AnalyticsEventDispatcher>();
    builder.Services.AddSingleton<IConnectionFactory>(sp =>
        new ConnectionFactory
        {
            HostName = config["RabbitMq__Host"] ?? "localhost",
            UserName = config["RabbitMq__Username"] ?? "guest",
            Password = config["RabbitMq__Password"] ?? "guest"
        });

    builder.Services.AddHostedService<AnalyticsEventConsumer>();

    builder.Services.AddJwtAuthentication(config);
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddScoped<ICurrentUserAccessor, HttpContextCurrentUserAccessor>();
    builder.Services.AddSingleton<ICorrelationIdAccessor, CorrelationIdAccessor>();

    builder.Services.AddControllers();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Analytics Service API",
            Version = "v1",
            Description = "Business intelligence and analytics for e-commerce orders"
        });
    });

    builder.Services.AddHealthChecks()
        .AddNpgSql(config.GetConnectionString("AnalyticsDb") ?? "Host=localhost;Port=5432;Database=analytics_db;Username=postgres;Password=postgres")
        .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy());

    builder.Services.AddOpenTelemetry()
        .WithTracing(tracerBuilder =>
            tracerBuilder
                .SetResourceBuilder(ResourceBuilder.CreateDefault()
                    .AddService("AnalyticsService"))
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddEntityFrameworkCoreInstrumentation()
                .AddOtlpExporter(opts =>
                {
                    opts.Endpoint = new Uri(config["Jaeger__Endpoint"] ?? "http://localhost:4317");
                }));

    var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Analytics Service API v1"));

    app.UseRouting();
    app.UseMiddleware<CorrelationIdMiddleware>();
    app.UseMiddleware<ErrorHandlingMiddleware>();
    app.UseMiddleware<LoggingMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapHealthChecks("/health", new HealthCheckOptions
    {
        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
    });

    app.MapMetrics();

    app.MapControllers();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AnalyticsDbContext>();
        await db.Database.MigrateAsync();
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
