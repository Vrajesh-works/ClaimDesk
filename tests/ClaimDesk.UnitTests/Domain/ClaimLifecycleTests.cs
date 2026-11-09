using ClaimDesk.Domain.Claims;
using ClaimDesk.Domain.Common;
using ClaimDesk.Domain.Users;

namespace ClaimDesk.UnitTests.Domain;

public class ClaimLifecycleTests
{
    private static readonly Guid ClaimantId = Guid.NewGuid();
    private static readonly Actor Claimant = new(ClaimantId, Roles.Claimant);
    private static readonly Actor OtherClaimant = new(Guid.NewGuid(), Roles.Claimant);
    private static readonly Actor Adjuster = new(Guid.NewGuid(), Roles.Adjuster);
    private static readonly Actor Admin = new(Guid.NewGuid(), Roles.Admin);
    private static DateTime Now => TestData.Now;

    [Fact]
    public void Filing_starts_in_submitted_with_one_audit_event()
    {
        var claim = TestData.NewClaim(ClaimantId);

        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        var submitted = Assert.Single(claim.Events);
        Assert.Equal(ClaimEventType.Submitted, submitted.Type);
        Assert.Equal(ClaimantId, submitted.ActorId);
    }

    [Fact]
    public void Full_happy_path_records_every_transition()
    {
        var claim = TestData.NewClaim(ClaimantId);

        claim.Apply(ClaimAction.StartReview, Adjuster, null, Now.AddHours(1));
        claim.Apply(ClaimAction.RequestInfo, Adjuster, "Need repair estimate", Now.AddHours(2));
        claim.Apply(ClaimAction.ProvideInfo, Claimant, "Estimate attached", Now.AddHours(3));
        claim.Apply(ClaimAction.Approve, Adjuster, null, Now.AddHours(4), 3_500m);
        claim.Apply(ClaimAction.MarkPaid, Admin, null, Now.AddHours(5));

        Assert.Equal(ClaimStatus.Paid, claim.Status);
        Assert.Equal(3_500m, claim.ApprovedAmount);
        Assert.Equal(Adjuster.UserId, claim.AssignedAdjusterId);
        var path = claim.Events.Where(e => e.Type == ClaimEventType.StatusChanged).Select(e => e.ToStatus).ToList();
        Assert.Equal(
            [ClaimStatus.InReview, ClaimStatus.MoreInfoNeeded, ClaimStatus.InReview, ClaimStatus.Approved, ClaimStatus.Paid],
            path);
        Assert.Equal(Now.AddHours(5), claim.LastActivityAt);
    }

    [Theory]
    [InlineData(ClaimAction.Approve)]
    [InlineData(ClaimAction.Deny)]
    [InlineData(ClaimAction.MarkPaid)]
    [InlineData(ClaimAction.ProvideInfo)]
    [InlineData(ClaimAction.RequestInfo)]
    public void Actions_not_valid_from_submitted_are_rejected(ClaimAction action)
    {
        var claim = TestData.NewClaim(ClaimantId);

        Assert.Throws<InvalidTransitionException>(() => claim.Apply(action, Admin, "note", Now));
        Assert.Equal(ClaimStatus.Submitted, claim.Status);
        Assert.Single(claim.Events);
    }

    [Fact]
    public void Claimant_cannot_start_review()
    {
        var claim = TestData.NewClaim(ClaimantId);

        Assert.Throws<ActionNotPermittedException>(() => claim.Apply(ClaimAction.StartReview, Claimant, null, Now));
    }

    [Fact]
    public void Only_owner_can_provide_info()
    {
        var claim = TestData.NewClaim(ClaimantId);
        claim.Apply(ClaimAction.StartReview, Adjuster, null, Now);
        claim.Apply(ClaimAction.RequestInfo, Adjuster, "Photos please", Now);

        Assert.Throws<ActionNotPermittedException>(() => claim.Apply(ClaimAction.ProvideInfo, OtherClaimant, "here", Now));
        Assert.Throws<ActionNotPermittedException>(() => claim.Apply(ClaimAction.ProvideInfo, Adjuster, "here", Now));
    }

    [Fact]
    public void Request_info_and_deny_require_a_note()
    {
        var claim = TestData.NewClaim(ClaimantId);
        claim.Apply(ClaimAction.StartReview, Adjuster, null, Now);

        Assert.Throws<DomainValidationException>(() => claim.Apply(ClaimAction.RequestInfo, Adjuster, "  ", Now));
        Assert.Throws<DomainValidationException>(() => claim.Apply(ClaimAction.Deny, Adjuster, null, Now));
    }

    [Fact]
    public void Adjuster_cannot_approve_above_limit_but_admin_can()
    {
        var claim = TestData.NewClaim(ClaimantId, 40_000m);
        claim.Apply(ClaimAction.StartReview, Adjuster, null, Now);

        Assert.Throws<ActionNotPermittedException>(() => claim.Apply(ClaimAction.Approve, Adjuster, null, Now));
        claim.Apply(ClaimAction.Approve, Admin, null, Now);

        Assert.Equal(ClaimStatus.Approved, claim.Status);
        Assert.Equal(40_000m, claim.ApprovedAmount);
    }

    [Fact]
    public void Approved_amount_cannot_exceed_claimed_amount()
    {
        var claim = TestData.NewClaim(ClaimantId, 1_000m);
        claim.Apply(ClaimAction.StartReview, Adjuster, null, Now);

        Assert.Throws<DomainValidationException>(() => claim.Apply(ClaimAction.Approve, Adjuster, null, Now, 1_500m));
    }

    [Fact]
    public void Only_admin_can_mark_paid()
    {
        var claim = TestData.NewClaim(ClaimantId);
        claim.Apply(ClaimAction.StartReview, Adjuster, null, Now);
        claim.Apply(ClaimAction.Approve, Adjuster, null, Now);

        Assert.Throws<ActionNotPermittedException>(() => claim.Apply(ClaimAction.MarkPaid, Adjuster, null, Now));
    }

    [Fact]
    public void Terminal_claims_reject_further_actions_and_evidence()
    {
        var claim = TestData.NewClaim(ClaimantId);
        claim.Apply(ClaimAction.StartReview, Adjuster, null, Now);
        claim.Apply(ClaimAction.Deny, Adjuster, "Policy lapsed", Now);

        foreach (var action in Enum.GetValues<ClaimAction>())
        {
            Assert.Throws<InvalidTransitionException>(() => claim.Apply(action, Admin, "x", Now));
        }

        var evidence = new Evidence { FileName = "a.pdf", ContentType = "application/pdf", StorageKey = "k", Sha256 = "h" };
        Assert.Throws<InvalidTransitionException>(() => claim.AddEvidence(evidence, Claimant, Now));
    }

    [Fact]
    public void Other_claimant_cannot_add_evidence()
    {
        var claim = TestData.NewClaim(ClaimantId);
        var evidence = new Evidence { FileName = "a.pdf", ContentType = "application/pdf", StorageKey = "k", Sha256 = "h" };

        Assert.Throws<ActionNotPermittedException>(() => claim.AddEvidence(evidence, OtherClaimant, Now));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Filing_rejects_non_positive_amounts(decimal amount)
    {
        Assert.Throws<DomainValidationException>(() => TestData.NewClaim(ClaimantId, amount));
    }

    [Fact]
    public void Filing_rejects_future_incident_date()
    {
        Assert.Throws<DomainValidationException>(() => Claim.File(
            ClaimantId, "N", "POL-1", Now.AddDays(1), 100m, "Future crash", TestData.LowRiskVehicle(), Now));
    }

    [Fact]
    public void Every_change_rotates_the_concurrency_version()
    {
        var claim = TestData.NewClaim(ClaimantId);
        var before = claim.Version;

        claim.Apply(ClaimAction.StartReview, Adjuster, null, Now);

        Assert.NotEqual(before, claim.Version);
    }
}
