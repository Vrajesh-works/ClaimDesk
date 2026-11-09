namespace ClaimDesk.Infrastructure.Seeding;

public sealed class SeedOptions
{
    public const string Section = "Seed";

    /// <summary>Create demo users and sample claims on startup when the database is empty.</summary>
    public bool Enabled { get; set; }

    /// <summary>Password for the demo accounts. Development only.</summary>
    public string DemoPassword { get; set; } = string.Empty;

    /// <summary>Number of synthetic, Kaggle-shaped claims to create.</summary>
    public int SampleClaims { get; set; } = 40;

    /// <summary>Optional path to the real Kaggle fraud_oracle.csv; when present it replaces synthetic claims.</summary>
    public string? KaggleCsvPath { get; set; }

    /// <summary>Maximum rows to import from the Kaggle CSV.</summary>
    public int KaggleMaxRows { get; set; } = 300;
}
