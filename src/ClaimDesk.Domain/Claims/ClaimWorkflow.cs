using ClaimDesk.Domain.Users;

namespace ClaimDesk.Domain.Claims;

/// <summary>One allowed edge of the claim state machine.</summary>
public sealed record ClaimTransition(
    ClaimAction Action,
    ClaimStatus From,
    ClaimStatus To,
    IReadOnlyList<string> AllowedRoles,
    bool RequiresNote,
    bool OwnerOnly = false);

/// <summary>
/// The claim lifecycle as data. This table is the single source of truth for which action
/// moves a claim between which states and which role may perform it.
/// </summary>
public static class ClaimWorkflow
{
    /// <summary>Approvals above this amount need an admin (four-eyes rule).</summary>
    public const decimal AdjusterApprovalLimit = 25_000m;

    public static readonly IReadOnlyList<ClaimTransition> Transitions =
    [
        new(ClaimAction.StartReview, ClaimStatus.Submitted, ClaimStatus.InReview, [Roles.Adjuster, Roles.Admin], RequiresNote: false),
        new(ClaimAction.RequestInfo, ClaimStatus.InReview, ClaimStatus.MoreInfoNeeded, [Roles.Adjuster, Roles.Admin], RequiresNote: true),
        new(ClaimAction.ProvideInfo, ClaimStatus.MoreInfoNeeded, ClaimStatus.InReview, [Roles.Claimant], RequiresNote: true, OwnerOnly: true),
        new(ClaimAction.Approve, ClaimStatus.InReview, ClaimStatus.Approved, [Roles.Adjuster, Roles.Admin], RequiresNote: false),
        new(ClaimAction.Deny, ClaimStatus.InReview, ClaimStatus.Denied, [Roles.Adjuster, Roles.Admin], RequiresNote: true),
        new(ClaimAction.MarkPaid, ClaimStatus.Approved, ClaimStatus.Paid, [Roles.Admin], RequiresNote: false),
    ];

    public static ClaimTransition? Find(ClaimAction action, ClaimStatus from) =>
        Transitions.FirstOrDefault(t => t.Action == action && t.From == from);

    /// <summary>Actions the actor could take on the claim right now (drives UI buttons).</summary>
    public static IReadOnlyList<ClaimAction> AvailableActions(Claim claim, Actor actor) =>
        Transitions
            .Where(t => t.From == claim.Status
                        && t.AllowedRoles.Contains(actor.Role)
                        && (!t.OwnerOnly || claim.ClaimantId == actor.UserId))
            .Select(t => t.Action)
            .ToList();
}
