using ClaimDesk.Domain.Claims;
using ClaimDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace ClaimDesk.Application.Abstractions;

public interface IClaimDeskDbContext
{
    DbSet<Claim> Claims { get; }
    DbSet<ClaimEvent> ClaimEvents { get; }
    DbSet<Evidence> Evidence { get; }
    DbSet<User> Users { get; }
    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
