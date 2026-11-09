using ClaimDesk.Api.Auth;
using ClaimDesk.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ClaimDesk.Api.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = Policies.Admin)]
public sealed class AdminController(IClaimDeskDbContext db) : ControllerBase
{
    [HttpGet("users")]
    public async Task<IReadOnlyList<UserInfo>> Users(CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .OrderBy(u => u.Email)
            .Select(u => new UserInfo(u.Id, u.Email, u.DisplayName, u.Role))
            .ToListAsync(ct);
}
