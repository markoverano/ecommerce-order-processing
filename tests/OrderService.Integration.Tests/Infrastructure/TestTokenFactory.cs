using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace OrderService.Integration.Tests.Infrastructure;

internal static class TestTokenFactory
{
    private const string TestSecret = "test-secret-key-for-integration-tests-min-32-chars";
    private const string Issuer = "test-issuer";
    private const string Audience = "test-audience";

    public static string CreateAdminToken(Guid? userId = null) =>
        CreateToken(userId ?? Guid.NewGuid(), ["admin"]);

    public static string CreateCustomerToken(Guid? userId = null) =>
        CreateToken(userId ?? Guid.NewGuid(), ["customer"]);

    private static string CreateToken(Guid userId, string[] roles)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
