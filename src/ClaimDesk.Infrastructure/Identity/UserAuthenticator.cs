using ClaimDesk.Application.Abstractions;
using ClaimDesk.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ClaimDesk.Infrastructure.Identity;

/// <summary>
/// Local username/password check for the demo identity provider. In Azure this whole class
/// goes away: Entra ID authenticates the user and the API only validates the token.
/// </summary>
public sealed class UserAuthenticator(IClaimDeskDbContext db, IPasswordHasher<User> hasher)
{
    public async Task<User?> AuthenticateAsync(string email, string password, CancellationToken ct)
    {
        var normalized = email.Trim().ToLowerInvariant();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
        {
            // Hash anyway so response time does not reveal whether the account exists.
            hasher.HashPassword(new User { Email = email, DisplayName = "", Role = "" }, password);
            return null;
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return result == PasswordVerificationResult.Failed ? null : user;
    }
}
