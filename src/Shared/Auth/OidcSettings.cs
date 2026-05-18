namespace ECommerceOrderProcessing.Shared.Auth;

/// <summary>Keycloak OIDC settings bound from the "Oidc" configuration section.</summary>
public sealed record OidcSettings
{
    public string Authority { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;

    /// <summary>Set to false in development when Keycloak is not behind HTTPS.</summary>
    public bool RequireHttpsMetadata { get; init; } = true;
}
