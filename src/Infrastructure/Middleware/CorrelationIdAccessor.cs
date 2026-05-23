using Microsoft.AspNetCore.Http;

namespace ECommerceOrderProcessing.Infrastructure.Middleware;

public sealed class CorrelationIdAccessor : ICorrelationIdAccessor
{
    private const string Key = "X-Correlation-ID";

    public Guid GetCorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue(Key, out var val) && val is Guid id)
            return id;
        return Guid.NewGuid();
    }
}
