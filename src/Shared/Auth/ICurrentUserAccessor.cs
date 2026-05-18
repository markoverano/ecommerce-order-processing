namespace ECommerceOrderProcessing.Shared.Auth;

/// <summary>Returns the authenticated caller's context for the current request, or null for anonymous requests.</summary>
public interface ICurrentUserAccessor
{
    CurrentUserContext? GetCurrentUser();
}
