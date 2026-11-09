using System.Net;
using ClaimDesk.Api.Controllers;
using ClaimDesk.Application.Claims;
using ClaimDesk.Application.Uploads;
using ClaimDesk.Domain.Claims;
using ClaimDesk.Infrastructure.Seeding;
using ClaimDesk.IntegrationTests.Infrastructure;

namespace ClaimDesk.IntegrationTests;

public class EvidenceTests(ClaimDeskApiFactory factory) : IClassFixture<ClaimDeskApiFactory>
{
    [Fact]
    public async Task Uploaded_file_round_trips_through_a_signed_link()
    {
        var id = await TestClaims.ArrangeAsync(factory, ClaimStatus.Submitted);
        using var claimant = factory.ClientFor(DemoUsers.Claimant);
        using var anonymous = factory.ClientFor(null);

        var evidence = await (await claimant.PostAsync($"/api/claims/{id}/evidence", TestClaims.FileForm(TestClaims.PdfBytes, "../../estimate.pdf"))).ReadAsync<EvidenceDto>();
        Assert.Equal("estimate.pdf", evidence.FileName);
        Assert.Equal("application/pdf", evidence.ContentType);

        var link = await (await claimant.GetAsync($"/api/claims/{id}/evidence/{evidence.Id}/link")).ReadAsync<EvidenceLinkDto>();
        using var download = await anonymous.GetAsync(link.Url);

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal(TestClaims.PdfBytes, await download.Content.ReadAsByteArrayAsync());
        Assert.Equal("nosniff", download.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Tampered_and_expired_links_are_rejected()
    {
        // Isolated factory: this test moves the clock forward.
        await using var isolated = new ClaimDeskApiFactory();
        var id = await TestClaims.ArrangeAsync(isolated, ClaimStatus.Submitted, withEvidence: true);
        using var claimant = isolated.ClientFor(DemoUsers.Claimant);
        using var anonymous = isolated.ClientFor(null);
        var evidence = (await (await claimant.GetAsync($"/api/claims/{id}/evidence")).ReadAsync<List<EvidenceDto>>()).Single();
        var link = await (await claimant.GetAsync($"/api/claims/{id}/evidence/{evidence.Id}/link")).ReadAsync<EvidenceLinkDto>();

        using var tampered = await anonymous.GetAsync(link.Url[..^3] + "abc");
        using var missing = await anonymous.GetAsync("/api/evidence/download");
        Assert.Equal(HttpStatusCode.Forbidden, tampered.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);

        isolated.Time.Advance(TimeSpan.FromMinutes(6));
        using var expired = await anonymous.GetAsync(link.Url);
        Assert.Equal(HttpStatusCode.Forbidden, expired.StatusCode);
    }

    [Theory]
    [InlineData("malware.exe", "Only PDF, PNG and JPEG")]
    [InlineData("photo.png", "does not match")]
    public async Task Rejects_disallowed_type_and_spoofed_content(string fileName, string expectedError)
    {
        var id = await TestClaims.ArrangeAsync(factory, ClaimStatus.Submitted);
        using var claimant = factory.ClientFor(DemoUsers.Claimant);

        using var response = await claimant.PostAsync($"/api/claims/{id}/evidence", TestClaims.FileForm(TestClaims.PdfBytes, fileName));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(expectedError, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rejects_files_over_the_size_limit()
    {
        var id = await TestClaims.ArrangeAsync(factory, ClaimStatus.Submitted);
        using var claimant = factory.ClientFor(DemoUsers.Claimant);
        var big = new byte[EvidencePolicy.MaxBytes + 1];
        TestClaims.PdfBytes.CopyTo(big, 0);

        using var response = await claimant.PostAsync($"/api/claims/{id}/evidence", TestClaims.FileForm(big, "huge.pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("10 MB", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Evidence_cannot_be_added_to_a_closed_claim()
    {
        var id = await TestClaims.ArrangeAsync(factory, ClaimStatus.Denied);
        using var claimant = factory.ClientFor(DemoUsers.Claimant);

        using var response = await claimant.PostAsync($"/api/claims/{id}/evidence", TestClaims.FileForm(TestClaims.PdfBytes, "late.pdf"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
