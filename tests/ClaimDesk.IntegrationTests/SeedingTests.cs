using ClaimDesk.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ClaimDesk.IntegrationTests;

public class SeedingTests
{
    private static string SampleCsv => Path.Combine(AppContext.BaseDirectory, "fraud_oracle_sample.csv");

    [Fact]
    public async Task Imports_claims_from_a_kaggle_shaped_csv()
    {
        await using var factory = new ClaimDeskApiFactory();
        factory.ExtraSettings["Seed:KaggleCsvPath"] = SampleCsv;

        var claims = await factory.WithDbAsync((db, _) => db.Claims.Include(c => c.Evidence).ToListAsync());

        Assert.Equal(5, claims.Count);
        Assert.All(claims, c => Assert.StartsWith("CLM-K", c.Number, StringComparison.Ordinal));
        var first = claims.Single(c => c.PolicyNumber == "POL-1");
        Assert.Equal("Honda", first.Vehicle.Make);
        Assert.False(first.Vehicle.PoliceReportFiled);
        Assert.Contains("No police report filed", first.RiskReasons, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Generates_synthetic_claims_in_every_status()
    {
        await using var factory = new ClaimDeskApiFactory();
        factory.ExtraSettings["Seed:SampleClaims"] = "40";

        var statuses = await factory.WithDbAsync((db, _) => db.Claims.Select(c => c.Status).Distinct().ToListAsync());

        Assert.Equal(6, statuses.Count);
    }
}
