using ClaimDesk.Domain.Users;
using Microsoft.AspNetCore.Authorization;

namespace ClaimDesk.Api.Auth;

/// <summary>Named authorization policies. Endpoints reference these, never raw role strings.</summary>
public static class Policies
{
    public const string Claimant = nameof(Claimant);
    public const string Staff = nameof(Staff);
    public const string Admin = nameof(Admin);
    public const string AnyRole = nameof(AnyRole);

    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(Claimant, p => p.RequireAuthenticatedUser().RequireRole(Roles.Claimant));
        options.AddPolicy(Staff, p => p.RequireAuthenticatedUser().RequireRole(Roles.Adjuster, Roles.Admin));
        options.AddPolicy(Admin, p => p.RequireAuthenticatedUser().RequireRole(Roles.Admin));
        options.AddPolicy(AnyRole, p => p.RequireAuthenticatedUser().RequireRole(Roles.All));

        // Deny by default: any endpoint without an explicit policy or [AllowAnonymous] needs a known role.
        options.FallbackPolicy = options.GetPolicy(AnyRole);
    }
}
