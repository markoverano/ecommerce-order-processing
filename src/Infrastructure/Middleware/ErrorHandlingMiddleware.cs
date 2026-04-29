using System.Net;
using System.Text.Json;
using ECommerceOrderProcessing.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ECommerceOrderProcessing.Infrastructure.Middleware;

/// <summary>Catches unhandled exceptions and translates them into a uniform JSON error response.</summary>
public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
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
        var correlationId = context.Items["X-Correlation-ID"] as Guid? ?? Guid.Empty;
        _logger.LogError(exception, "Unhandled exception. CorrelationId={CorrelationId}", correlationId);

        var (statusCode, code) = exception switch
        {
            ArgumentException => (HttpStatusCode.BadRequest, "BAD_REQUEST"),
            KeyNotFoundException => (HttpStatusCode.NotFound, "NOT_FOUND"),
            InvalidOperationException => (HttpStatusCode.Conflict, "CONFLICT"),
            _ => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR")
        };

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var error = new ErrorResponse(code, "An error occurred while processing your request.");
        var body = JsonSerializer.Serialize(error, _jsonOptions);
        await context.Response.WriteAsync(body);
    }
}
