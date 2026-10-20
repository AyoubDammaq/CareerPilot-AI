using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CareerPilot.API.Auth;

public sealed record IssuedToken(string Token, DateTimeOffset ExpiresAt);

public sealed class TokenService(IOptions<AuthOptions> options, TimeProvider clock)
{
    private readonly AuthOptions _options = options.Value;

    /// <summary>Constant-time comparison of the supplied password with the configured one.</summary>
    public bool VerifyPassword(string? supplied)
    {
        // Hash both so the comparison is fixed-length regardless of input length.
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(supplied ?? ""));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(_options.Password));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    public IssuedToken Issue()
    {
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(_options.TokenLifetimeMinutes);
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.JwtKey));

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: [new Claim(JwtRegisteredClaimNames.Sub, "owner"), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))],
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
