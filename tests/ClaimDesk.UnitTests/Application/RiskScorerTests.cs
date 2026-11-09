using ClaimDesk.Application.Risk;
using ClaimDesk.Domain.Claims;

namespace ClaimDesk.UnitTests.Application;

public class RiskScorerTests
{
    private readonly RiskScorer _scorer = new();

    [Fact]
    public void Clean_claim_scores_zero_and_is_not_flagged()
    {
        var result = _scorer.Assess(TestData.LowRiskVehicle(), 3_000m);

        Assert.Equal(0, result.Score);
        Assert.Empty(result.Reasons);
        Assert.False(result.Flagged);
    }

    [Fact]
    public void New_policy_with_no_police_report_and_address_change_is_flagged()
    {
        var vehicle = TestData.LowRiskVehicle();
        vehicle.DaysPolicyAccident = DaysPolicyAccident.OneToSeven;
        vehicle.AddressChange = AddressChange.UnderSixMonths;
        vehicle.PoliceReportFiled = false;

        var result = _scorer.Assess(vehicle, 3_000m);

        Assert.Equal(60, result.Score);
        Assert.True(result.Flagged);
        Assert.Contains("No police report filed", result.Reasons);
    }

    [Fact]
    public void Score_is_capped_at_100()
    {
        var vehicle = new VehicleDetails
        {
            Make = "Porsche",
            Category = VehicleCategory.Sport,
            Fault = Fault.PolicyHolder,
            AgentType = AgentType.External,
            PoliceReportFiled = false,
            WitnessPresent = false,
            DaysPolicyAccident = DaysPolicyAccident.None,
            AddressChange = AddressChange.UnderSixMonths,
            PastClaims = PastClaims.MoreThanFour,
        };

        var result = _scorer.Assess(vehicle, 90_000m);

        Assert.Equal(100, result.Score);
        Assert.Equal(9, result.Reasons.Count);
    }

    [Fact]
    public void Threshold_is_inclusive()
    {
        var vehicle = TestData.LowRiskVehicle();
        vehicle.DaysPolicyAccident = DaysPolicyAccident.None;   // 25
        vehicle.AddressChange = AddressChange.UnderSixMonths;   // 20
        vehicle.AgentType = AgentType.External;                 // 5

        var result = _scorer.Assess(vehicle, 1_000m);

        Assert.Equal(RiskScorer.FlagThreshold, result.Score);
        Assert.True(result.Flagged);
    }

    [Theory]
    [InlineData(PastClaims.None, 0)]
    [InlineData(PastClaims.One, 0)]
    [InlineData(PastClaims.TwoToFour, 8)]
    [InlineData(PastClaims.MoreThanFour, 15)]
    public void Past_claims_are_banded(PastClaims pastClaims, int expected)
    {
        var vehicle = TestData.LowRiskVehicle();
        vehicle.PastClaims = pastClaims;

        Assert.Equal(expected, _scorer.Assess(vehicle, 1_000m).Score);
    }
}
