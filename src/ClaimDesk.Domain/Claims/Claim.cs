using ClaimDesk.Domain.Common;
using ClaimDesk.Domain.Users;

namespace ClaimDesk.Domain.Claims;

/// <summary>Insurance claim aggregate. All state changes go through methods that write audit events.</summary>
public sealed class Claim
{
    private readonly List<ClaimEvent> _events = [];
    private readonly List<Evidence> _evidence = [];

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Number { get; private set; } = string.Empty;
    public Guid ClaimantId { get; private set; }
    public ClaimStatus Status { get; private set; }
    public string PolicyNumber { get; private set; } = string.Empty;
    public DateTime IncidentDate { get; private set; }
    public decimal ClaimedAmount { get; private set; }
    public decimal? ApprovedAmount { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public VehicleDetails Vehicle { get; private set; } = null!;

    public int RiskScore { get; private set; }
    public string RiskReasons { get; private set; } = string.Empty;
    public bool FlaggedForReview { get; private set; }

    public Guid? AssignedAdjusterId { get; private set; }
    public DateTime SubmittedAt { get; private set; }
    public DateTime LastActivityAt { get; private set; }
    public DateTime? LastSlaReminderAt { get; private set; }
    public DateTime? LastStuckAlertAt { get; private set; }

    /// <summary>Optimistic concurrency token, regenerated on every change.</summary>
    public Guid Version { get; private set; } = Guid.NewGuid();

    public IReadOnlyList<ClaimEvent> Events => _events;
    public IReadOnlyList<Evidence> Evidence => _evidence;

    private Claim()
    {
    }

    public static Claim File(
        Guid claimantId,
        string number,
        string policyNumber,
        DateTime incidentDate,
        decimal claimedAmount,
        string description,
        VehicleDetails vehicle,
        DateTime now)
    {
        if (claimedAmount <= 0)
        {
            throw new DomainValidationException("Claimed amount must be greater than zero.");
        }

        if (incidentDate > now)
        {
            throw new DomainValidationException("Incident date cannot be in the future.");
        }

        if (string.IsNullOrWhiteSpace(policyNumber))
        {
            throw new DomainValidationException("Policy number is required.");
        }

        var claim = new Claim
        {
            ClaimantId = claimantId,
            Number = number,
            PolicyNumber = policyNumber.Trim(),
            IncidentDate = incidentDate,
            ClaimedAmount = claimedAmount,
            Description = description.Trim(),
            Vehicle = vehicle,
            Status = ClaimStatus.Submitted,
            SubmittedAt = now,
            LastActivityAt = now,
        };
        claim.Record(ClaimEventType.Submitted, null, ClaimStatus.Submitted, new Actor(claimantId, Roles.Claimant), null, now);
        return claim;
    }

    /// <summary>Applies a lifecycle action after checking state, role, ownership and business rules.</summary>
    public void Apply(ClaimAction action, Actor actor, string? note, DateTime now, decimal? approvedAmount = null)
    {
        var transition = ClaimWorkflow.Find(action, Status)
            ?? throw new InvalidTransitionException($"Cannot {action} a claim that is {Status}.");

        if (!transition.AllowedRoles.Contains(actor.Role))
        {
            throw new ActionNotPermittedException($"Role {actor.Role} cannot {action}.");
        }

        if (transition.OwnerOnly && actor.UserId != ClaimantId)
        {
            throw new ActionNotPermittedException($"Only the claimant who filed the claim can {action}.");
        }

        if (transition.RequiresNote && string.IsNullOrWhiteSpace(note))
        {
            throw new DomainValidationException($"A note is required to {action}.");
        }

        if (action == ClaimAction.Approve)
        {
            var amount = approvedAmount ?? ClaimedAmount;
            if (amount <= 0 || amount > ClaimedAmount)
            {
                throw new DomainValidationException("Approved amount must be positive and not exceed the claimed amount.");
            }

            if (amount > ClaimWorkflow.AdjusterApprovalLimit && actor.Role != Roles.Admin)
            {
                throw new ActionNotPermittedException(
                    $"Approvals above {ClaimWorkflow.AdjusterApprovalLimit:N0} require an admin.");
            }

            ApprovedAmount = amount;
        }

        if (action == ClaimAction.StartReview && actor.Role == Roles.Adjuster)
        {
            AssignedAdjusterId = actor.UserId;
        }

        var from = Status;
        Status = transition.To;
        Record(ClaimEventType.StatusChanged, from, Status, actor, note?.Trim(), now);
    }

    public void ApplyRiskAssessment(int score, IEnumerable<string> reasons, bool flagged, DateTime now)
    {
        RiskScore = Math.Clamp(score, 0, 100);
        RiskReasons = string.Join("; ", reasons);
        FlaggedForReview = flagged;
        var note = $"Risk score {RiskScore}{(flagged ? " (flagged for review)" : string.Empty)}"
                   + (RiskReasons.Length > 0 ? $": {RiskReasons}" : string.Empty);
        Record(ClaimEventType.RiskScored, null, null, null, note, now, touchActivity: false);
    }

    public Evidence AddEvidence(Evidence evidence, Actor actor, DateTime now)
    {
        if (Status.IsTerminal())
        {
            throw new InvalidTransitionException($"Cannot add evidence to a claim that is {Status}.");
        }

        if (actor.Role == Roles.Claimant && actor.UserId != ClaimantId)
        {
            throw new ActionNotPermittedException("Only the claimant who filed the claim can add evidence.");
        }

        evidence.ClaimId = Id;
        evidence.UploadedById = actor.UserId;
        evidence.UploadedAt = now;
        _evidence.Add(evidence);
        Record(ClaimEventType.EvidenceAdded, null, null, actor, $"Uploaded {evidence.FileName}", now);
        return evidence;
    }

    public void RecordSlaReminder(string message, DateTime now)
    {
        LastSlaReminderAt = now;
        Record(ClaimEventType.SlaReminder, null, null, null, message, now, touchActivity: false);
    }

    public void RecordStuckAlert(string message, DateTime now)
    {
        LastStuckAlertAt = now;
        Record(ClaimEventType.StuckAlert, null, null, null, message, now, touchActivity: false);
    }

    private void Record(
        ClaimEventType type,
        ClaimStatus? from,
        ClaimStatus? to,
        Actor? actor,
        string? note,
        DateTime now,
        bool touchActivity = true)
    {
        _events.Add(ClaimEvent.Create(Id, type, from, to, actor?.UserId, actor?.Role ?? "System", note, now));
        if (touchActivity)
        {
            LastActivityAt = now;
        }

        Version = Guid.NewGuid();
    }
}
