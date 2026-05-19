namespace ECommerceOrderProcessing.Infrastructure.Auth;

/// <summary>Keycloak client credentials settings for service-to-service token acquisition.</summary>
public sealed record ClientCredentialsSettings
{
    public string TokenEndpoint { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
}
