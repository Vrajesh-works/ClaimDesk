using System.Security.Claims;
using System.Text;
using ClaimDesk.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ClaimDesk.Api.Auth;

public sealed record IssuedToken(string AccessToken, DateTime ExpiresAt);

/// <summary>
/// Local token issuer that mimics the shape of an Entra ID access token (sub, name, email,
/// roles). Only used by the demo login endpoint and tests.
/// </summary>
public sealed class TokenIssuer(IOptions<JwtOptions> options, TimeProvider time)
{
    public static SymmetricSecurityKey CreateKey(string signingKey) => new(Encoding.UTF8.GetBytes(signingKey));

    public IssuedToken Issue(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var jwt = options.Value;
        var now = time.GetUtcNow().UtcDateTime;
        var expires = now.Add(jwt.Lifetime);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", user.Id.ToString()),
                new Claim("name", user.DisplayName),
                new Claim("email", user.Email),
                new Claim("roles", user.Role),
            ]),
            SigningCredentials = new SigningCredentials(CreateKey(jwt.SigningKey), SecurityAlgorithms.HmacSha256),
        };

        return new IssuedToken(new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
