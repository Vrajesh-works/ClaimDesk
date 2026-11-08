using System.Net.Http.Json;
using ClaimDesk.Domain.Claims;
using ClaimDesk.Domain.Users;
using ClaimDesk.Infrastructure.Seeding;
using ClaimDesk.Infrastructure.Storage;
using ClaimDesk.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClaimDesk.IntegrationTests;

public enum Caller
{
    Anonymous,
    Owner,
    OtherClaimant,
    Adjuster,
    Admin,
}

/// <summary>
/// One authorization test per endpoint and caller. Every route the API exposes must appear in
/// <see cref="Cases"/>; <see cref="Every_endpoint_is_covered_by_the_matrix"/> fails otherwise.
/// Each test arranges a claim in the state the endpoint needs, so "allowed" means the call
/// actually succeeds, not merely that it was not rejected.
/// </summary>
public class AuthorizationMatrixTests(ClaimDeskApiFactory factory) : IClassFixture<ClaimDeskApiFactory>
{
    private sealed record Context(Guid ClaimId, Guid EvidenceId, string DownloadToken);

    private sealed record EndpointCase(
        string Method,
        string Route,
        ClaimStatus Arrange,
        Func<Context, string> Url,
        Func<HttpContent?> Body,
        int Anonymous,
        int Owner,
        int OtherClaimant,
        int Adjuster,
        int Admin)
    {
        public int Expected(Caller caller) => caller switch
        {
            Caller.Anonymous => Anonymous,
            Caller.Owner => Owner,
            Caller.OtherClaimant => OtherClaimant,
            Caller.Adjuster => Adjuster,
            _ => Admin,
        };
    }

    private static JsonContent Note() => JsonContent.Create(new { note = "authorization matrix" });

    private static HttpContent? None() => null;

    private static readonly EndpointCase[] Cases =
    [
        new("POST", "api/auth/login", ClaimStatus.Submitted, _ => "/api/auth/login",
            () => JsonContent.Create(new { email = DemoUsers.Claimant.Email, password = ClaimDeskApiFactory.DemoPassword }),
            200, 200, 200, 200, 200),
        new("GET", "api/auth/me", ClaimStatus.Submitted, _ => "/api/auth/me", None, 401, 200, 200, 200, 200),
        new("POST", "api/claims", ClaimStatus.Submitted, _ => "/api/claims",
            () => JsonContent.Create(TestClaims.NewRequest(), options: TestClaims.Json),
            401, 201, 201, 403, 403),
        new("GET", "api/claims", ClaimStatus.Submitted, _ => "/api/claims", None, 401, 200, 200, 200, 200),
        new("GET", "api/claims/{id:guid}", ClaimStatus.Submitted, c => $"/api/claims/{c.ClaimId}", None, 401, 200, 403, 200, 200),
        new("GET", "api/claims/{id:guid}/history", ClaimStatus.Submitted, c => $"/api/claims/{c.ClaimId}/history", None, 401, 200, 403, 200, 200),
        new("POST", "api/claims/{id:guid}/start-review", ClaimStatus.Submitted, c => $"/api/claims/{c.ClaimId}/start-review", Note, 401, 403, 403, 200, 200),
        new("POST", "api/claims/{id:guid}/request-info", ClaimStatus.InReview, c => $"/api/claims/{c.ClaimId}/request-info", Note, 401, 403, 403, 200, 200),
        new("POST", "api/claims/{id:guid}/provide-info", ClaimStatus.MoreInfoNeeded, c => $"/api/claims/{c.ClaimId}/provide-info", Note, 401, 200, 403, 403, 403),
        new("POST", "api/claims/{id:guid}/approve", ClaimStatus.InReview, c => $"/api/claims/{c.ClaimId}/approve", Note, 401, 403, 403, 200, 200),
        new("POST", "api/claims/{id:guid}/deny", ClaimStatus.InReview, c => $"/api/claims/{c.ClaimId}/deny", Note, 401, 403, 403, 200, 200),
        new("POST", "api/claims/{id:guid}/mark-paid", ClaimStatus.Approved, c => $"/api/claims/{c.ClaimId}/mark-paid", Note, 401, 403, 403, 403, 200),
        new("POST", "api/claims/{id:guid}/evidence", ClaimStatus.Submitted, c => $"/api/claims/{c.ClaimId}/evidence",
            () => TestClaims.FileForm(TestClaims.PdfBytes, "estimate.pdf"),
            401, 201, 403, 201, 403),
        new("GET", "api/claims/{id:guid}/evidence", ClaimStatus.Submitted, c => $"/api/claims/{c.ClaimId}/evidence", None, 401, 200, 403, 200, 200),
        new("GET", "api/claims/{id:guid}/evidence/{evidenceId:guid}/link", ClaimStatus.Submitted,
            c => $"/api/claims/{c.ClaimId}/evidence/{c.EvidenceId}/link", None, 401, 200, 403, 200, 200),
        // Signed links are bearer-less by design (like SAS URLs): the token is the credential.
        new("GET", "api/evidence/download", ClaimStatus.Submitted, c => $"/api/evidence/download?token={c.DownloadToken}", None, 200, 200, 200, 200, 200),
        new("GET", "api/queue", ClaimStatus.Submitted, _ => "/api/queue", None, 401, 403, 403, 200, 200),
        new("GET", "api/notifications", ClaimStatus.Submitted, _ => "/api/notifications", None, 401, 403, 403, 200, 200),
        new("GET", "api/admin/users", ClaimStatus.Submitted, _ => "/api/admin/users", None, 401, 403, 403, 403, 200),
        new("GET", "health/live", ClaimStatus.Submitted, _ => "/health/live", None, 200, 200, 200, 200, 200),
        new("GET", "health/ready", ClaimStatus.Submitted, _ => "/health/ready", None, 200, 200, 200, 200, 200),
    ];

    public static TheoryData<string, Caller> Matrix()
    {
        var data = new TheoryData<string, Caller>();
        foreach (var c in Cases)
        {
            foreach (var caller in Enum.GetValues<Caller>())
            {
                data.Add($"{c.Method} {c.Route}", caller);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public async Task Endpoint_enforces_expected_access(string endpoint, Caller caller)
    {
        var @case = Cases.Single(c => $"{c.Method} {c.Route}" == endpoint);
        var context = await ArrangeAsync(@case.Arrange);
        using var client = factory.ClientFor(UserFor(caller));
        using var request = new HttpRequestMessage(new HttpMethod(@case.Method), @case.Url(context)) { Content = @case.Body() };

        using var response = await client.SendAsync(request);

        Assert.True(
            (int)response.StatusCode == @case.Expected(caller),
            $"{endpoint} as {caller}: expected {@case.Expected(caller)}, got {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public void Every_endpoint_is_covered_by_the_matrix()
    {
        var exposed = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(m => $"{m} {e.RoutePattern.RawText!.TrimStart('/')}"))
            .ToHashSet();
        var covered = Cases.Select(c => $"{c.Method} {c.Route}").ToHashSet();

        Assert.Empty(exposed.Except(covered));
        Assert.Empty(covered.Except(exposed));
    }

    private static User? UserFor(Caller caller) => caller switch
    {
        Caller.Owner => DemoUsers.Claimant,
        Caller.OtherClaimant => DemoUsers.Claimant2,
        Caller.Adjuster => DemoUsers.Adjuster,
        Caller.Admin => DemoUsers.Admin,
        _ => null,
    };

    private async Task<Context> ArrangeAsync(ClaimStatus status)
    {
        var claimId = await TestClaims.ArrangeAsync(factory, status, withEvidence: true);
        return await factory.WithDbAsync(async (db, services) =>
        {
            var evidence = await db.Evidence.SingleAsync(e => e.ClaimId == claimId);
            var token = services.GetRequiredService<SignedLinkProtector>()
                .Protect(evidence.StorageKey, evidence.FileName, evidence.ContentType, TimeSpan.FromMinutes(5));
            return new Context(claimId, evidence.Id, token);
        });
    }
}
