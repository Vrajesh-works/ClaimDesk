using ClaimDesk.Domain.Users;

namespace ClaimDesk.Infrastructure.Seeding;

public static class DemoUsers
{
    public static readonly User Claimant = new() { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Email = "claimant@claimdesk.local", DisplayName = "Casey Claimant", Role = Roles.Claimant };
    public static readonly User Claimant2 = new() { Id = Guid.Parse("11111111-1111-1111-1111-222222222222"), Email = "claimant2@claimdesk.local", DisplayName = "Jordan Driver", Role = Roles.Claimant };
    public static readonly User Adjuster = new() { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Email = "adjuster@claimdesk.local", DisplayName = "Alex Adjuster", Role = Roles.Adjuster };
    public static readonly User Admin = new() { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Email = "admin@claimdesk.local", DisplayName = "Avery Admin", Role = Roles.Admin };

    public static IReadOnlyList<User> All => [Claimant, Claimant2, Adjuster, Admin];
}
