using System.Net;

namespace ECommerceOrderProcessing.Infrastructure.Middleware;

/// <summary>
/// Fallback mapper covering the common .NET exception types that the infrastructure
/// layer already understands. Service-specific mappers registered before this one
/// take precedence.
/// </summary>
public sealed class DefaultExceptionMapper : IExceptionMapper
{
    public bool CanMap(Exception exception) => true;

    public (HttpStatusCode StatusCode, string ErrorCode) Map(Exception exception) => exception switch
    {
        ArgumentException => (HttpStatusCode.BadRequest, "VALIDATION_FAILED"),
        KeyNotFoundException => (HttpStatusCode.NotFound, "RESOURCE_NOT_FOUND"),
        InvalidOperationException => (HttpStatusCode.Conflict, "BUSINESS_RULE_VIOLATION"),
        _ => (HttpStatusCode.InternalServerError, "INTERNAL_ERROR")
    };
}
