using ClaimDesk.Domain.Claims;

namespace ClaimDesk.UnitTests;

internal static class TestData
{
    public static readonly DateTime Now = new(2026, 11, 8, 12, 0, 0, DateTimeKind.Utc);

    public static VehicleDetails LowRiskVehicle() => new()
    {
        Make = "Honda",
        Category = VehicleCategory.Sedan,
        VehicleYear = 2020,
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

    public static Claim NewClaim(Guid claimantId, decimal amount = 4_000m) => Claim.File(
        claimantId,
        "CLM-TEST-1",
        "POL-12345",
        Now.AddDays(-2),
        amount,
        "Rear-ended at a stop light.",
        LowRiskVehicle(),
        Now);
}
