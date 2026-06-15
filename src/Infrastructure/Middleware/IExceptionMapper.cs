using System.Net;

namespace ECommerceOrderProcessing.Infrastructure.Middleware;

/// <summary>
/// Maps a caught exception to an HTTP status code and error code string.
/// Register service-specific implementations to extend error handling without
/// modifying the shared <see cref="ErrorHandlingMiddleware"/>.
/// </summary>
public interface IExceptionMapper
{
    bool CanMap(Exception exception);
    (HttpStatusCode StatusCode, string ErrorCode) Map(Exception exception);
}
