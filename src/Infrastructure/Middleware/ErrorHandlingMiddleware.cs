using System.Net;
using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.Serialization;
using ECommerceOrderProcessing.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ECommerceOrderProcessing.Infrastructure.Middleware;

/// <summary>Catches unhandled exceptions and translates them into a uniform JSON error response.</summary>
public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ICorrelationIdAccessor correlationIdAccessor,
        ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _correlationIdAccessor = correlationIdAccessor;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = _correlationIdAccessor.GetCorrelationId(context);
        _logger.LogError(exception, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);

        var (statusCode, code) = exception switch
        {
            ArgumentException => (HttpStatusCode.BadRequest, "VALIDATION_FAILED"),
            KeyNotFoundException => (HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND"),
            InvalidOperationException => (HttpStatusCode.Conflict, "BUSINESS_RULE_VIOLATION"),
            _ => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR")
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var error = new ErrorResponse(code, "An error occurred while processing your request.");
        var body = JsonSerializer.Serialize(error, InfrastructureJsonOptions.Default);
        await context.Response.WriteAsync(body);
    }
}
