using Microsoft.AspNetCore.Http;

namespace ECommerceOrderProcessing.Infrastructure.Middleware;

/// <summary>Retrieves the correlation ID stored by <see cref="CorrelationIdMiddleware"/>, or generates a fresh one.</summary>
public interface ICorrelationIdAccessor
{
    Guid GetCorrelationId(HttpContext context);
}
