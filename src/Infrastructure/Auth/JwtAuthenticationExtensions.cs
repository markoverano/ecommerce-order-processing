using ECommerceOrderProcessing.Shared.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace ECommerceOrderProcessing.Infrastructure.Auth;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("Oidc").Get<OidcSettings>() ?? new OidcSettings();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opts =>
            {
                opts.Authority = settings.Authority;
                opts.Audience = settings.Audience;
                opts.RequireHttpsMetadata = settings.RequireHttpsMetadata;
                opts.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = !string.IsNullOrEmpty(settings.Audience),
                    ValidateLifetime = true,
                    // Keycloak maps realm roles to the "roles" claim in the access token.
                    RoleClaimType = "roles"
                };
            });

        services.AddAuthorization();

        return services;
    }
}
