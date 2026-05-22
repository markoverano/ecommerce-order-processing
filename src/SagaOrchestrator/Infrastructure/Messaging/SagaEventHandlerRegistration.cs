using Microsoft.Extensions.DependencyInjection;
using SagaOrchestrator.Application.EventHandlers;

namespace SagaOrchestrator.Infrastructure.Messaging;

public static class SagaEventHandlerRegistration
{
    public static IServiceCollection AddSagaEventHandlers(this IServiceCollection services)
    {
        services.AddScoped<ISagaEventHandler, OrderCreatedHandler>();
        services.AddScoped<ISagaEventHandler, PaymentProcessedHandler>();
        services.AddScoped<ISagaEventHandler, PaymentFailedHandler>();
        services.AddScoped<ISagaEventHandler, PaymentRefundedHandler>();
        services.AddScoped<ISagaEventHandler, StockReservedHandler>();
        services.AddScoped<ISagaEventHandler, OutOfStockHandler>();
        services.AddScoped<ISagaEventHandler, StockReleasedHandler>();
        services.AddScoped<ISagaEventHandler, ShipmentCreatedHandler>();
        services.AddScoped<ISagaEventHandler, ShipmentFailedHandler>();
        services.AddScoped<ISagaEventHandler, NotificationSentHandler>();
        services.AddScoped<SagaEventDispatcher>();
        return services;
    }
}
