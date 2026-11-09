namespace ClaimDesk.Domain.Claims;

public enum ClaimStatus
{
    Submitted,
    InReview,
    MoreInfoNeeded,
    Approved,
    Denied,
    Paid,
}

public enum ClaimAction
{
    StartReview,
    RequestInfo,
    ProvideInfo,
    Approve,
    Deny,
    MarkPaid,
}

public enum ClaimEventType
{
    Submitted,
    StatusChanged,
    EvidenceAdded,
    RiskScored,
    SlaReminder,
    StuckAlert,
}

public static class ClaimStatusExtensions
{
    public static bool IsTerminal(this ClaimStatus status) =>
        status is ClaimStatus.Denied or ClaimStatus.Paid;

    /// <summary>Open claims still need work from someone.</summary>
    public static bool IsOpen(this ClaimStatus status) =>
        status is ClaimStatus.Submitted or ClaimStatus.InReview or ClaimStatus.MoreInfoNeeded;
}
