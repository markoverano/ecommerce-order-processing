using ECommerceOrderProcessing.Shared.ValueObjects;

namespace ECommerceOrderProcessing.Shared.Auth;

/// <summary>Identity of the authenticated caller, derived from JWT sub/email/roles claims.</summary>
public sealed record CurrentUserContext(
    CustomerId UserId,
    string Email,
    IReadOnlyList<string> Roles);
