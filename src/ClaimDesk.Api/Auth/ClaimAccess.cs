using System.Security.Claims;
using ClaimDesk.Domain.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using DomainClaim = ClaimDesk.Domain.Claims.Claim;

namespace ClaimDesk.Api.Auth;

/// <summary>Resource-based operations on a single claim.</summary>
public static class ClaimOperations
{
    public static readonly OperationAuthorizationRequirement View = new() { Name = nameof(View) };
    public static readonly OperationAuthorizationRequirement AddEvidence = new() { Name = nameof(AddEvidence) };
}

/// <summary>
/// Claimants may only touch claims they filed. Adjusters and admins may view any claim;
/// evidence can be added by the owner or an adjuster.
/// </summary>
public sealed class ClaimAccessHandler : AuthorizationHandler<OperationAuthorizationRequirement, DomainClaim>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        DomainClaim resource)
    {
        var user = context.User;
        var isOwner = user.IsInRole(Roles.Claimant) && user.GetUserId() == resource.ClaimantId;

        var allowed = requirement.Name switch
        {
            nameof(ClaimOperations.View) => isOwner || user.IsInRole(Roles.Adjuster) || user.IsInRole(Roles.Admin),
            nameof(ClaimOperations.AddEvidence) => isOwner || user.IsInRole(Roles.Adjuster),
            _ => false,
        };

        if (allowed)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out var id) ? id : Guid.Empty;
    }

    /// <summary>The caller as a domain actor. Uses the first recognised role in the token.</summary>
    public static Actor ToActor(this ClaimsPrincipal user)
    {
        var role = Roles.All.FirstOrDefault(user.IsInRole) ?? string.Empty;
        return new Actor(user.GetUserId(), role);
    }
}
