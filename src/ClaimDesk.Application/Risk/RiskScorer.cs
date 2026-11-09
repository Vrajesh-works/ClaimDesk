using ClaimDesk.Domain.Claims;

namespace ClaimDesk.Application.Risk;

public sealed record RiskRule(string Name, int Weight, string Reason);

public sealed record RiskAssessment(int Score, IReadOnlyList<string> Reasons, bool Flagged);

public interface IRiskScorer
{
    RiskAssessment Assess(VehicleDetails vehicle, decimal claimedAmount);
}

/// <summary>
/// Explainable weighted rules over fields from the Kaggle vehicle claim fraud dataset.
/// Weights are starting assumptions; tools/fraud_lift.py recalibrates them against FraudFound_P.
/// </summary>
public sealed class RiskScorer : IRiskScorer
{
    public const int FlagThreshold = 50;
    public const decimal HighValueAmount = 20_000m;

    public RiskAssessment Assess(VehicleDetails vehicle, decimal claimedAmount)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        var hits = new List<RiskRule>();

        if (vehicle.DaysPolicyAccident is DaysPolicyAccident.None or DaysPolicyAccident.OneToSeven)
        {
            hits.Add(new("NewPolicy", 25, "Policy started within 7 days of the accident"));
        }

        if (vehicle.AddressChange == AddressChange.UnderSixMonths)
        {
            hits.Add(new("RecentAddressChange", 20, "Address changed less than 6 months before the claim"));
        }

        if (!vehicle.PoliceReportFiled)
        {
            hits.Add(new("NoPoliceReport", 15, "No police report filed"));
        }

        if (vehicle.PastClaims == PastClaims.MoreThanFour)
        {
            hits.Add(new("ManyPastClaims", 15, "More than 4 past claims"));
        }
        else if (vehicle.PastClaims == PastClaims.TwoToFour)
        {
            hits.Add(new("SeveralPastClaims", 8, "2 to 4 past claims"));
        }

        if (!vehicle.WitnessPresent)
        {
            hits.Add(new("NoWitness", 10, "No witness present"));
        }

        if (vehicle.Fault == Fault.PolicyHolder)
        {
            hits.Add(new("PolicyHolderAtFault", 10, "Policy holder at fault"));
        }

        if (vehicle.Category == VehicleCategory.Sport)
        {
            hits.Add(new("SportVehicle", 10, "Sport vehicle"));
        }

        if (claimedAmount > HighValueAmount)
        {
            hits.Add(new("HighValue", 10, $"Claimed amount over {HighValueAmount:N0}"));
        }

        if (vehicle.AgentType == AgentType.External)
        {
            hits.Add(new("ExternalAgent", 5, "Policy sold by an external agent"));
        }

        var score = Math.Min(100, hits.Sum(h => h.Weight));
        return new RiskAssessment(score, hits.Select(h => h.Reason).ToList(), score >= FlagThreshold);
    }
}
