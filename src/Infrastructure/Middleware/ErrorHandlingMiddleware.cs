using System.Text.Json;
using ECommerceOrderProcessing.Infrastructure.Serialization;
using ECommerceOrderProcessing.Shared.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ECommerceOrderProcessing.Infrastructure.Middleware;

/// <summary>
/// Catches unhandled exceptions and translates them into a uniform JSON error response.
/// Uses all registered <see cref="IExceptionMapper"/> implementations in order; the
/// <see cref="DefaultExceptionMapper"/> acts as the final fallback.
/// </summary>
public sealed class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ICorrelationIdAccessor _correlationIdAccessor;
    private readonly IReadOnlyList<IExceptionMapper> _mappers;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(
        RequestDelegate next,
        ICorrelationIdAccessor correlationIdAccessor,
        IEnumerable<IExceptionMapper> mappers,
        ILogger<ErrorHandlingMiddleware> logger)
    {
        _next = next;
        _correlationIdAccessor = correlationIdAccessor;
        _mappers = mappers.ToList().AsReadOnly();
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

        var mapper = _mappers.FirstOrDefault(m => m.CanMap(exception)) ?? new DefaultExceptionMapper();
        var (statusCode, code) = mapper.Map(exception);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var error = new ErrorResponse(code, exception.Message);
        var body = JsonSerializer.Serialize(error, InfrastructureJsonOptions.Default);
        await context.Response.WriteAsync(body);
    }
}
