using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ClaimDesk.Application.Claims;
using ClaimDesk.Domain.Claims;
using ClaimDesk.Domain.Users;
using ClaimDesk.Infrastructure.Seeding;

namespace ClaimDesk.IntegrationTests.Infrastructure;

internal static class TestClaims
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static readonly byte[] PdfBytes = "%PDF-1.7\n1 0 obj\n<<>>\nendobj\n%%EOF\n"u8.ToArray();

    public static FileClaimRequest NewRequest(decimal amount = 4_200m) => new()
    {
        PolicyNumber = "POL-778899",
        IncidentDate = DateTime.UtcNow.Date.AddDays(-3),
        ClaimedAmount = amount,
        Description = "Rear-ended at a red light on Main Street.",
        Make = "Toyota",
        VehicleYear = 2021,
        Category = VehicleCategory.Sedan,
        AccidentArea = AccidentArea.Urban,
        Fault = Fault.ThirdParty,
        BasePolicy = BasePolicy.Collision,
        AgentType = AgentType.Internal,
        PoliceReportFiled = true,
        WitnessPresent = true,
        DaysPolicyAccident = DaysPolicyAccident.MoreThanThirty,
        AddressChange = AddressChange.NoChange,
        PastClaims = PastClaims.None,
    };

    /// <summary>Creates a claim owned by the demo claimant and walks it to the requested status.</summary>
    public static Task<Guid> ArrangeAsync(ClaimDeskApiFactory factory, ClaimStatus status, bool withEvidence = false) =>
        factory.WithDbAsync(async (db, services) =>
        {
            var service = (ClaimService)services.GetService(typeof(ClaimService))!;
            var owner = new Actor(DemoUsers.Claimant.Id, Roles.Claimant);
            var adjuster = new Actor(DemoUsers.Adjuster.Id, Roles.Adjuster);
            var admin = new Actor(DemoUsers.Admin.Id, Roles.Admin);
            var claim = await service.FileAsync(owner, NewRequest(), CancellationToken.None);

            if (withEvidence)
            {
                using var pdf = new MemoryStream(PdfBytes);
                await service.AddEvidenceAsync(claim, owner, "estimate.pdf", pdf, PdfBytes.Length, CancellationToken.None);
            }

            var path = status switch
            {
                ClaimStatus.Submitted => [],
                ClaimStatus.InReview => [(ClaimAction.StartReview, adjuster)],
                ClaimStatus.MoreInfoNeeded => [(ClaimAction.StartReview, adjuster), (ClaimAction.RequestInfo, adjuster)],
                ClaimStatus.Approved => [(ClaimAction.StartReview, adjuster), (ClaimAction.Approve, adjuster)],
                ClaimStatus.Denied => [(ClaimAction.StartReview, adjuster), (ClaimAction.Deny, adjuster)],
                ClaimStatus.Paid => new (ClaimAction, Actor)[] { (ClaimAction.StartReview, adjuster), (ClaimAction.Approve, adjuster), (ClaimAction.MarkPaid, admin) },
                _ => throw new ArgumentOutOfRangeException(nameof(status)),
            };

            foreach (var (action, actor) in path)
            {
                await service.TransitionAsync(claim, action, actor, new TransitionRequest { Note = "arranged by test" }, CancellationToken.None);
            }

            return claim.Id;
        });

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(Json))!;

    public static MultipartFormDataContent FileForm(byte[] bytes, string fileName)
    {
        var form = new MultipartFormDataContent();
        form.Add(new ByteArrayContent(bytes), "file", fileName);
        return form;
    }
}
