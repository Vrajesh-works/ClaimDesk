using System.Net;
using System.Net.Http.Json;
using ClaimDesk.Application.Claims;
using ClaimDesk.Domain.Claims;
using ClaimDesk.Infrastructure.Seeding;
using ClaimDesk.IntegrationTests.Infrastructure;

namespace ClaimDesk.IntegrationTests;

public class LifecycleTests(ClaimDeskApiFactory factory) : IClassFixture<ClaimDeskApiFactory>
{
    [Fact]
    public async Task Full_lifecycle_over_http_records_complete_audit_history()
    {
        using var claimant = factory.ClientFor(DemoUsers.Claimant);
        using var adjuster = factory.ClientFor(DemoUsers.Adjuster);
        using var admin = factory.ClientFor(DemoUsers.Admin);

        var filed = await (await claimant.PostAsJsonAsync("/api/claims", TestClaims.NewRequest(), TestClaims.Json)).ReadAsync<ClaimDetailDto>();
        Assert.Equal(ClaimStatus.Submitted, filed.Status);
        Assert.Empty(filed.AvailableActions);

        using (var upload = await claimant.PostAsync($"/api/claims/{filed.Id}/evidence", TestClaims.FileForm(TestClaims.PdfBytes, "police-report.pdf")))
        {
            Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        }

        var queue = await (await adjuster.GetAsync("/api/queue")).ReadAsync<List<ClaimSummaryDto>>();
        Assert.Contains(queue, c => c.Id == filed.Id);

        await Transition(adjuster, filed.Id, "start-review", new { }, ClaimStatus.InReview);
        await Transition(adjuster, filed.Id, "request-info", new { note = "Please add the repair estimate." }, ClaimStatus.MoreInfoNeeded);

        var forClaimant = await (await claimant.GetAsync($"/api/claims/{filed.Id}")).ReadAsync<ClaimDetailDto>();
        Assert.Equal([ClaimAction.ProvideInfo], forClaimant.AvailableActions);

        await Transition(claimant, filed.Id, "provide-info", new { note = "Estimate uploaded." }, ClaimStatus.InReview);
        await Transition(adjuster, filed.Id, "approve", new { approvedAmount = 4_000m }, ClaimStatus.Approved);
        var paid = await Transition(admin, filed.Id, "mark-paid", new { }, ClaimStatus.Paid);
        Assert.Equal(4_000m, paid.ApprovedAmount);

        var history = await (await claimant.GetAsync($"/api/claims/{filed.Id}/history")).ReadAsync<List<ClaimEventDto>>();
        Assert.Equal(
            [
                ClaimEventType.Submitted, ClaimEventType.RiskScored, ClaimEventType.EvidenceAdded,
                ClaimEventType.StatusChanged, ClaimEventType.StatusChanged, ClaimEventType.StatusChanged,
                ClaimEventType.StatusChanged, ClaimEventType.StatusChanged,
            ],
            history.Select(h => h.Type));
        Assert.Equal("Please add the repair estimate.", history[4].Note);
        Assert.Equal("Admin", history[^1].ActorRole);

        var afterPaid = await (await adjuster.GetAsync("/api/queue")).ReadAsync<List<ClaimSummaryDto>>();
        Assert.DoesNotContain(afterPaid, c => c.Id == filed.Id);
    }

    [Fact]
    public async Task Invalid_transition_returns_409_and_leaves_history_unchanged()
    {
        var id = await TestClaims.ArrangeAsync(factory, ClaimStatus.Submitted);
        using var adjuster = factory.ClientFor(DemoUsers.Adjuster);

        using var response = await adjuster.PostAsJsonAsync($"/api/claims/{id}/approve", new { });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var history = await (await adjuster.GetAsync($"/api/claims/{id}/history")).ReadAsync<List<ClaimEventDto>>();
        Assert.DoesNotContain(history, h => h.Type == ClaimEventType.StatusChanged);
    }

    [Fact]
    public async Task Missing_note_returns_400()
    {
        var id = await TestClaims.ArrangeAsync(factory, ClaimStatus.InReview);
        using var adjuster = factory.ClientFor(DemoUsers.Adjuster);

        using var response = await adjuster.PostAsJsonAsync($"/api/claims/{id}/deny", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Adjuster_cannot_approve_above_limit_admin_can()
    {
        using var claimant = factory.ClientFor(DemoUsers.Claimant);
        using var adjuster = factory.ClientFor(DemoUsers.Adjuster);
        using var admin = factory.ClientFor(DemoUsers.Admin);
        var filed = await (await claimant.PostAsJsonAsync("/api/claims", TestClaims.NewRequest(48_000m), TestClaims.Json)).ReadAsync<ClaimDetailDto>();
        await Transition(adjuster, filed.Id, "start-review", new { }, ClaimStatus.InReview);

        using var denied = await adjuster.PostAsJsonAsync($"/api/claims/{filed.Id}/approve", new { });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        await Transition(admin, filed.Id, "approve", new { }, ClaimStatus.Approved);
    }

    [Fact]
    public async Task Filing_validates_input()
    {
        using var claimant = factory.ClientFor(DemoUsers.Claimant);
        var request = TestClaims.NewRequest();
        request.Description = "short";
        request.ClaimedAmount = 0;

        using var response = await claimant.PostAsJsonAsync("/api/claims", request, TestClaims.Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task High_risk_claim_is_flagged_and_ranked_first_in_queue()
    {
        using var claimant = factory.ClientFor(DemoUsers.Claimant);
        using var adjuster = factory.ClientFor(DemoUsers.Adjuster);
        var risky = TestClaims.NewRequest();
        risky.DaysPolicyAccident = DaysPolicyAccident.OneToSeven;
        risky.AddressChange = AddressChange.UnderSixMonths;
        risky.PoliceReportFiled = false;
        risky.WitnessPresent = false;
        risky.Fault = Fault.PolicyHolder;
        risky.Category = VehicleCategory.Sport;

        await claimant.PostAsJsonAsync("/api/claims", TestClaims.NewRequest(), TestClaims.Json);
        var flagged = await (await claimant.PostAsJsonAsync("/api/claims", risky, TestClaims.Json)).ReadAsync<ClaimDetailDto>();

        Assert.True(flagged.FlaggedForReview);
        Assert.Equal(90, flagged.RiskScore);
        Assert.Contains("No police report filed", flagged.RiskReasons);

        var queue = await (await adjuster.GetAsync("/api/queue")).ReadAsync<List<ClaimSummaryDto>>();
        Assert.Equal(flagged.Id, queue[0].Id);
        Assert.True(queue.Zip(queue.Skip(1)).All(p => p.First.RiskScore >= p.Second.RiskScore));
    }

    [Fact]
    public async Task Claimant_list_only_contains_own_claims()
    {
        await TestClaims.ArrangeAsync(factory, ClaimStatus.Submitted);
        using var other = factory.ClientFor(DemoUsers.Claimant2);

        var mine = await (await other.GetAsync("/api/claims")).ReadAsync<List<ClaimSummaryDto>>();

        Assert.Empty(mine);
    }

    [Fact]
    public async Task Login_rejects_wrong_password_and_accepts_right_one()
    {
        using var client = factory.ClientFor(null);

        using var bad = await client.PostAsJsonAsync("/api/auth/login", new { email = DemoUsers.Admin.Email, password = "nope-nope" });
        using var good = await client.PostAsJsonAsync("/api/auth/login", new { email = "ADMIN@claimdesk.local", password = ClaimDeskApiFactory.DemoPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
    }

    private static async Task<ClaimDetailDto> Transition(HttpClient client, Guid id, string action, object body, ClaimStatus expected)
    {
        using var response = await client.PostAsJsonAsync($"/api/claims/{id}/{action}", body);
        Assert.True(response.IsSuccessStatusCode, $"{action}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
        var detail = await response.ReadAsync<ClaimDetailDto>();
        Assert.Equal(expected, detail.Status);
        return detail;
    }
}
