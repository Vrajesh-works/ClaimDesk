namespace ClaimDesk.Domain.Claims;

// Categorical fields shaped like the Kaggle "Vehicle Claim Fraud Detection" dataset
// (fraud_oracle.csv). Enum member names map to the CSV band labels in KaggleBands.

public enum AccidentArea { Urban, Rural }

public enum Fault { PolicyHolder, ThirdParty }

public enum VehicleCategory { Sedan, Sport, Utility }

public enum BasePolicy { Liability, Collision, AllPerils }

public enum AgentType { Internal, External }

/// <summary>Days between policy start and the accident (<c>Days_Policy_Accident</c>).</summary>
public enum DaysPolicyAccident { None, OneToSeven, EightToFifteen, FifteenToThirty, MoreThanThirty }

/// <summary>Time between an address change and the claim (<c>AddressChange_Claim</c>).</summary>
public enum AddressChange { NoChange, UnderSixMonths, OneYear, TwoToThreeYears, FourToEightYears }

/// <summary>Previous claims on the policy (<c>PastNumberOfClaims</c>).</summary>
public enum PastClaims { None, One, TwoToFour, MoreThanFour }

/// <summary>Vehicle and incident facts captured when a claim is filed.</summary>
public sealed class VehicleDetails
{
    public required string Make { get; set; }
    public VehicleCategory Category { get; set; }
    public int VehicleYear { get; set; }
    public AccidentArea AccidentArea { get; set; }
    public Fault Fault { get; set; }
    public BasePolicy BasePolicy { get; set; }
    public AgentType AgentType { get; set; }
    public bool PoliceReportFiled { get; set; }
    public bool WitnessPresent { get; set; }
    public DaysPolicyAccident DaysPolicyAccident { get; set; }
    public AddressChange AddressChange { get; set; }
    public PastClaims PastClaims { get; set; }
}
