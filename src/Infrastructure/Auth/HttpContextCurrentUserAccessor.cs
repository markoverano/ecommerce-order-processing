using System.Security.Claims;
using ECommerceOrderProcessing.Shared.Auth;
using ECommerceOrderProcessing.Shared.ValueObjects;
using Microsoft.AspNetCore.Http;

namespace ECommerceOrderProcessing.Infrastructure.Auth;

public sealed class HttpContextCurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public CurrentUserContext? GetCurrentUser()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        if (user?.Identity?.IsAuthenticated != true)
            return null;

        var sub = user.FindFirstValue("sub");
        if (sub is null || !Guid.TryParse(sub, out var userId))
            return null;

        var email = user.FindFirstValue("email") ?? string.Empty;
        var roles = user.FindAll("roles").Select(c => c.Value).ToList().AsReadOnly();

        return new CurrentUserContext(new CustomerId(userId), email, roles);
    }
}
