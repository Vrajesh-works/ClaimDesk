using ClaimDesk.Api.Auth;
using ClaimDesk.Application.Abstractions;
using ClaimDesk.Application.Claims;
using ClaimDesk.Domain.Claims;
using ClaimDesk.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaimDesk.Api.Controllers;

[ApiController]
[Route("api")]
public sealed class QueueController(ClaimService claims, IClaimDeskDbContext db) : ControllerBase
{
    /// <summary>Open claims ordered by risk score (highest first), then by age.</summary>
    [HttpGet("queue")]
    [Authorize(Policy = Policies.Staff)]
    public Task<IReadOnlyList<ClaimSummaryDto>> Queue(CancellationToken ct) => claims.QueueAsync(ct);

    /// <summary>SLA reminders and stuck-claim alerts for the caller's role.</summary>
    [HttpGet("notifications")]
    [Authorize(Policy = Policies.Staff)]
    public async Task<IReadOnlyList<NotificationDto>> Notifications(CancellationToken ct)
    {
        var role = User.ToActor().Role;
        var roles = role == Roles.Admin ? new[] { Roles.Admin, Roles.Adjuster } : [role];
        var rows = await db.Notifications.AsNoTracking()
            .Where(n => roles.Contains(n.RecipientRole))
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        return rows.Select(n => new NotificationDto(n.Id, n.ClaimId, n.Kind, n.RecipientRole, n.Message, n.CreatedAt)).ToList();
    }
}

public sealed record NotificationDto(Guid Id, Guid ClaimId, NotificationKind Kind, string RecipientRole, string Message, DateTime CreatedAt);
