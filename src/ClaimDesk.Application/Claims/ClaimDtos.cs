using System.ComponentModel.DataAnnotations;
using ClaimDesk.Domain.Claims;
using ClaimDesk.Domain.Users;

namespace ClaimDesk.Application.Claims;

public sealed class FileClaimRequest
{
    [Required, StringLength(30, MinimumLength = 3)]
    public string PolicyNumber { get; set; } = string.Empty;

    [Required]
    public DateTime IncidentDate { get; set; }

    [Range(1, 1_000_000)]
    public decimal ClaimedAmount { get; set; }

    [Required, StringLength(2000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    [Required, StringLength(40)]
    public string Make { get; set; } = string.Empty;

    [Range(1980, 2100)]
    public int VehicleYear { get; set; }

    public VehicleCategory Category { get; set; }
    public AccidentArea AccidentArea { get; set; }
    public Fault Fault { get; set; }
    public BasePolicy BasePolicy { get; set; }
    public AgentType AgentType { get; set; }
    public bool PoliceReportFiled { get; set; }
    public bool WitnessPresent { get; set; }
    public DaysPolicyAccident DaysPolicyAccident { get; set; }
    public AddressChange AddressChange { get; set; }
    public PastClaims PastClaims { get; set; }

    public VehicleDetails ToVehicle() => new()
    {
        Make = Make.Trim(),
        VehicleYear = VehicleYear,
        Category = Category,
        AccidentArea = AccidentArea,
        Fault = Fault,
        BasePolicy = BasePolicy,
        AgentType = AgentType,
        PoliceReportFiled = PoliceReportFiled,
        WitnessPresent = WitnessPresent,
        DaysPolicyAccident = DaysPolicyAccident,
        AddressChange = AddressChange,
        PastClaims = PastClaims,
    };
}

public sealed class TransitionRequest
{
    [StringLength(2000)]
    public string? Note { get; set; }

    [Range(0.01, 1_000_000)]
    public decimal? ApprovedAmount { get; set; }
}

public sealed record ClaimSummaryDto(
    Guid Id,
    string Number,
    ClaimStatus Status,
    string PolicyNumber,
    decimal ClaimedAmount,
    int RiskScore,
    bool FlaggedForReview,
    DateTime SubmittedAt,
    DateTime LastActivityAt)
{
    public static ClaimSummaryDto From(Claim c) => new(
        c.Id, c.Number, c.Status, c.PolicyNumber, c.ClaimedAmount, c.RiskScore, c.FlaggedForReview, c.SubmittedAt, c.LastActivityAt);
}

public sealed record EvidenceDto(Guid Id, string FileName, string ContentType, long SizeBytes, DateTime UploadedAt)
{
    public static EvidenceDto From(Evidence e) => new(e.Id, e.FileName, e.ContentType, e.SizeBytes, e.UploadedAt);
}

public sealed record ClaimDetailDto(
    Guid Id,
    string Number,
    Guid ClaimantId,
    ClaimStatus Status,
    string PolicyNumber,
    DateTime IncidentDate,
    decimal ClaimedAmount,
    decimal? ApprovedAmount,
    string Description,
    VehicleDetails Vehicle,
    int RiskScore,
    IReadOnlyList<string> RiskReasons,
    bool FlaggedForReview,
    Guid? AssignedAdjusterId,
    DateTime SubmittedAt,
    DateTime LastActivityAt,
    IReadOnlyList<EvidenceDto> Evidence,
    IReadOnlyList<ClaimAction> AvailableActions)
{
    public static ClaimDetailDto From(Claim c, Actor actor) => new(
        c.Id,
        c.Number,
        c.ClaimantId,
        c.Status,
        c.PolicyNumber,
        c.IncidentDate,
        c.ClaimedAmount,
        c.ApprovedAmount,
        c.Description,
        c.Vehicle,
        c.RiskScore,
        c.RiskReasons.Split("; ", StringSplitOptions.RemoveEmptyEntries),
        c.FlaggedForReview,
        c.AssignedAdjusterId,
        c.SubmittedAt,
        c.LastActivityAt,
        c.Evidence.OrderBy(e => e.UploadedAt).Select(EvidenceDto.From).ToList(),
        ClaimWorkflow.AvailableActions(c, actor));
}

public sealed record ClaimEventDto(
    long Id,
    ClaimEventType Type,
    ClaimStatus? FromStatus,
    ClaimStatus? ToStatus,
    Guid? ActorId,
    string ActorRole,
    string? Note,
    DateTime OccurredAt)
{
    public static ClaimEventDto From(ClaimEvent e) => new(
        e.Id, e.Type, e.FromStatus, e.ToStatus, e.ActorId, e.ActorRole, e.Note, e.OccurredAt);
}
