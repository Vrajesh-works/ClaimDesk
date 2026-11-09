namespace ClaimDesk.Domain.Users;

/// <summary>
/// Application roles. The names match the Entra ID app role values the API would receive
/// in the <c>roles</c> claim, so swapping the identity provider does not change authorization.
/// </summary>
public static class Roles
{
    public const string Claimant = "Claimant";
    public const string Adjuster = "Adjuster";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Claimant, Adjuster, Admin];
}

/// <summary>Who is performing an action.</summary>
public sealed record Actor(Guid UserId, string Role);
