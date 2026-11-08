using ClaimDesk.Domain.Claims;

namespace ClaimDesk.Infrastructure.Seeding;

/// <summary>Maps the band labels used in the Kaggle fraud_oracle.csv file to domain enums.</summary>
public static class KaggleBands
{
    public static AccidentArea AccidentArea(string v) => v.Trim() == "Rural" ? Domain.Claims.AccidentArea.Rural : Domain.Claims.AccidentArea.Urban;

    public static Fault Fault(string v) => v.Trim() == "Policy Holder" ? Domain.Claims.Fault.PolicyHolder : Domain.Claims.Fault.ThirdParty;

    public static VehicleCategory Category(string v) => v.Trim() switch
    {
        "Sport" => VehicleCategory.Sport,
        "Utility" => VehicleCategory.Utility,
        _ => VehicleCategory.Sedan,
    };

    public static BasePolicy BasePolicy(string v) => v.Trim() switch
    {
        "Liability" => Domain.Claims.BasePolicy.Liability,
        "All Perils" => Domain.Claims.BasePolicy.AllPerils,
        _ => Domain.Claims.BasePolicy.Collision,
    };

    public static AgentType AgentType(string v) => v.Trim() == "External" ? Domain.Claims.AgentType.External : Domain.Claims.AgentType.Internal;

    public static DaysPolicyAccident DaysPolicyAccident(string v) => v.Trim() switch
    {
        "none" => Domain.Claims.DaysPolicyAccident.None,
        "1 to 7" => Domain.Claims.DaysPolicyAccident.OneToSeven,
        "8 to 15" => Domain.Claims.DaysPolicyAccident.EightToFifteen,
        "15 to 30" => Domain.Claims.DaysPolicyAccident.FifteenToThirty,
        _ => Domain.Claims.DaysPolicyAccident.MoreThanThirty,
    };

    public static AddressChange AddressChange(string v) => v.Trim() switch
    {
        "under 6 months" => Domain.Claims.AddressChange.UnderSixMonths,
        "1 year" => Domain.Claims.AddressChange.OneYear,
        "2 to 3 years" => Domain.Claims.AddressChange.TwoToThreeYears,
        "4 to 8 years" => Domain.Claims.AddressChange.FourToEightYears,
        _ => Domain.Claims.AddressChange.NoChange,
    };

    public static PastClaims PastClaims(string v) => v.Trim() switch
    {
        "1" => Domain.Claims.PastClaims.One,
        "2 to 4" => Domain.Claims.PastClaims.TwoToFour,
        "more than 4" => Domain.Claims.PastClaims.MoreThanFour,
        _ => Domain.Claims.PastClaims.None,
    };

    public static bool YesNo(string v) => v.Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>The dataset has no claim amount; approximate one from the VehiclePrice band midpoint.</summary>
    public static decimal VehiclePriceMidpoint(string v) => v.Trim() switch
    {
        "less than 20000" => 15_000m,
        "20000 to 29000" => 24_500m,
        "30000 to 39000" => 34_500m,
        "40000 to 59000" => 49_500m,
        "60000 to 69000" => 64_500m,
        "more than 69000" => 75_000m,
        _ => 25_000m,
    };

    public static int VehicleAgeYears(string v) => v.Trim() switch
    {
        "new" => 0,
        "2 years" => 2,
        "3 years" => 3,
        "4 years" => 4,
        "5 years" => 5,
        "6 years" => 6,
        "7 years" => 7,
        "more than 7" => 9,
        _ => 5,
    };
}
