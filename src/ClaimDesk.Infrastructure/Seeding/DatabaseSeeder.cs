using ClaimDesk.Application.Risk;
using ClaimDesk.Domain.Claims;
using ClaimDesk.Domain.Users;
using ClaimDesk.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaimDesk.Infrastructure.Seeding;

/// <summary>
/// Creates demo users and Kaggle-shaped sample claims. Imports the real Kaggle CSV when
/// <see cref="SeedOptions.KaggleCsvPath"/> points at it; otherwise generates synthetic rows
/// with a fixed random seed so every environment sees the same data.
/// </summary>
public sealed class DatabaseSeeder(
    ClaimDeskDbContext db,
    IPasswordHasher<User> hasher,
    IRiskScorer riskScorer,
    IOptions<SeedOptions> options,
    TimeProvider time,
    ILogger<DatabaseSeeder> logger)
{
    private static readonly string[] Makes = ["Honda", "Toyota", "Pontiac", "Mazda", "Chevrolet", "Accura", "Ford", "VW", "Volkswagen", "Saab", "Mercury", "Saturn", "Dodge", "Nisson", "BMW"];

    private static readonly string[] Descriptions =
    [
        "Rear-ended while stopped at a traffic light.",
        "Side-swiped in a parking lot, other driver left the scene.",
        "Hit a deer on a rural road at night.",
        "Collision at an intersection, other driver ran a red light.",
        "Vehicle damaged by hail during a storm.",
        "Backed into a pole in a parking garage.",
        "Single-vehicle accident after sliding on ice.",
        "Front bumper damage after a low-speed collision.",
    ];

    public async Task SeedAsync(CancellationToken ct)
    {
        var settings = options.Value;
        await SeedUsersAsync(settings, ct);

        if (await db.Claims.AnyAsync(ct))
        {
            return;
        }

        var claims = !string.IsNullOrWhiteSpace(settings.KaggleCsvPath) && File.Exists(settings.KaggleCsvPath)
            ? ImportKaggle(settings.KaggleCsvPath, settings.KaggleMaxRows)
            : GenerateSynthetic(settings.SampleClaims);

        db.Claims.AddRange(claims);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} claims", claims.Count);
    }

    private async Task SeedUsersAsync(SeedOptions settings, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.DemoPassword))
        {
            throw new InvalidOperationException("Seed:DemoPassword must be set when seeding is enabled.");
        }

        foreach (var template in DemoUsers.All)
        {
            var user = new User { Id = template.Id, Email = template.Email, DisplayName = template.DisplayName, Role = template.Role };
            user.PasswordHash = hasher.HashPassword(user, settings.DemoPassword);
            db.Users.Add(user);
        }

        await db.SaveChangesAsync(ct);
    }

    private List<Claim> GenerateSynthetic(int count)
    {
        var random = new Random(20261108);
        var now = time.GetUtcNow().UtcDateTime;
        var claims = new List<Claim>(count);

        for (var i = 0; i < count; i++)
        {
            // Rough category frequencies from the Kaggle dataset: most claims are clean.
            var vehicle = new VehicleDetails
            {
                Make = Makes[random.Next(Makes.Length)],
                Category = Pick(random, (VehicleCategory.Sedan, 62), (VehicleCategory.Sport, 35), (VehicleCategory.Utility, 3)),
                VehicleYear = 2012 + random.Next(14),
                AccidentArea = random.Next(100) < 90 ? AccidentArea.Urban : AccidentArea.Rural,
                Fault = random.Next(100) < 73 ? Fault.PolicyHolder : Fault.ThirdParty,
                BasePolicy = Pick(random, (BasePolicy.Collision, 39), (BasePolicy.Liability, 32), (BasePolicy.AllPerils, 29)),
                AgentType = random.Next(100) < 98 ? AgentType.External : AgentType.Internal,
                PoliceReportFiled = random.Next(100) < 25,
                WitnessPresent = random.Next(100) < 20,
                DaysPolicyAccident = Pick(random, (DaysPolicyAccident.MoreThanThirty, 85), (DaysPolicyAccident.FifteenToThirty, 5), (DaysPolicyAccident.EightToFifteen, 4), (DaysPolicyAccident.OneToSeven, 3), (DaysPolicyAccident.None, 3)),
                AddressChange = Pick(random, (AddressChange.NoChange, 80), (AddressChange.FourToEightYears, 8), (AddressChange.TwoToThreeYears, 6), (AddressChange.OneYear, 3), (AddressChange.UnderSixMonths, 3)),
                PastClaims = Pick(random, (PastClaims.TwoToFour, 36), (PastClaims.None, 28), (PastClaims.One, 23), (PastClaims.MoreThanFour, 13)),
            };

            var submittedAt = now.AddHours(-random.Next(1, 24 * 9));
            var amount = Math.Round((decimal)(500 + (random.NextDouble() * random.NextDouble() * 45_000)), 2);
            var claimant = i % 3 == 0 ? DemoUsers.Claimant2 : DemoUsers.Claimant;
            var claim = Claim.File(
                claimant.Id,
                $"CLM-{submittedAt:yyyyMMdd}-{i + 1:D4}",
                $"POL-{100000 + random.Next(900000)}",
                submittedAt.AddDays(-random.Next(1, 20)),
                amount,
                Descriptions[random.Next(Descriptions.Length)],
                vehicle,
                submittedAt);
            Score(claim, submittedAt);
            Advance(claim, i, submittedAt);
            claims.Add(claim);
        }

        return claims;
    }

    private List<Claim> ImportKaggle(string path, int maxRows)
    {
        var lines = File.ReadLines(path).ToList();
        var header = lines[0].Split(',').Select((name, index) => (name, index)).ToDictionary(x => x.name.Trim(), x => x.index);
        var now = time.GetUtcNow().UtcDateTime;
        var random = new Random(15421);
        var claims = new List<Claim>();

        foreach (var line in lines.Skip(1).Take(maxRows))
        {
            var cells = line.Split(',');
            string Col(string name) => cells[header[name]];

            var submittedAt = now.AddHours(-random.Next(1, 24 * 9));
            var vehicle = new VehicleDetails
            {
                Make = Col("Make"),
                Category = KaggleBands.Category(Col("VehicleCategory")),
                VehicleYear = submittedAt.Year - KaggleBands.VehicleAgeYears(Col("AgeOfVehicle")),
                AccidentArea = KaggleBands.AccidentArea(Col("AccidentArea")),
                Fault = KaggleBands.Fault(Col("Fault")),
                BasePolicy = KaggleBands.BasePolicy(Col("BasePolicy")),
                AgentType = KaggleBands.AgentType(Col("AgentType")),
                PoliceReportFiled = KaggleBands.YesNo(Col("PoliceReportFiled")),
                WitnessPresent = KaggleBands.YesNo(Col("WitnessPresent")),
                DaysPolicyAccident = KaggleBands.DaysPolicyAccident(Col("Days_Policy_Accident")),
                AddressChange = KaggleBands.AddressChange(Col("AddressChange_Claim")),
                PastClaims = KaggleBands.PastClaims(Col("PastNumberOfClaims")),
            };

            // The CSV has no amount or dates: derive an amount from the vehicle price band and
            // place the claim in the last few days so the queue and SLA worker have live data.
            var amount = Math.Round(KaggleBands.VehiclePriceMidpoint(Col("VehiclePrice")) * (decimal)(0.05 + (random.NextDouble() * 0.4)), 2);
            var claim = Claim.File(
                DemoUsers.Claimant.Id,
                $"CLM-K{Col("PolicyNumber").Trim().PadLeft(6, '0')}",
                $"POL-{Col("PolicyNumber").Trim()}",
                submittedAt.AddDays(-random.Next(1, 20)),
                amount,
                $"Imported from Kaggle fraud_oracle.csv ({Col("Month")} {Col("Year")}, {Col("AccidentArea")} accident).",
                vehicle,
                submittedAt);
            Score(claim, submittedAt);
            claims.Add(claim);
        }

        return claims;
    }

    private void Score(Claim claim, DateTime at)
    {
        var risk = riskScorer.Assess(claim.Vehicle, claim.ClaimedAmount);
        claim.ApplyRiskAssessment(risk.Score, risk.Reasons, risk.Flagged, at);
    }

    /// <summary>Moves some sample claims along the lifecycle so every status is represented.</summary>
    private static void Advance(Claim claim, int index, DateTime submittedAt)
    {
        var adjuster = new Actor(DemoUsers.Adjuster.Id, Roles.Adjuster);
        var admin = new Actor(DemoUsers.Admin.Id, Roles.Admin);
        var claimant = new Actor(claim.ClaimantId, Roles.Claimant);
        var t = submittedAt;

        switch (index % 8)
        {
            case 2:
                claim.Apply(ClaimAction.StartReview, adjuster, null, t = t.AddHours(2));
                break;
            case 3:
                claim.Apply(ClaimAction.StartReview, adjuster, null, t = t.AddHours(2));
                claim.Apply(ClaimAction.RequestInfo, adjuster, "Please upload the repair estimate.", t.AddHours(3));
                break;
            case 4:
                claim.Apply(ClaimAction.StartReview, adjuster, null, t = t.AddHours(1));
                claim.Apply(ClaimAction.Approve, claim.ClaimedAmount > 25_000m ? admin : adjuster, null, t.AddHours(5));
                break;
            case 5:
                claim.Apply(ClaimAction.StartReview, adjuster, null, t = t.AddHours(1));
                claim.Apply(ClaimAction.RequestInfo, adjuster, "Need photos of the damage.", t = t.AddHours(2));
                claim.Apply(ClaimAction.ProvideInfo, claimant, "Photos uploaded.", t = t.AddHours(4));
                claim.Apply(ClaimAction.Approve, admin, null, t = t.AddHours(1));
                claim.Apply(ClaimAction.MarkPaid, admin, null, t.AddHours(1));
                break;
            case 6:
                claim.Apply(ClaimAction.StartReview, adjuster, null, t = t.AddHours(1));
                claim.Apply(ClaimAction.Deny, adjuster, "Damage predates the policy start date.", t.AddHours(6));
                break;
            default:
                break;
        }
    }

    private static T Pick<T>(Random random, params (T Value, int Weight)[] options)
    {
        var roll = random.Next(options.Sum(o => o.Weight));
        foreach (var (value, weight) in options)
        {
            if (roll < weight)
            {
                return value;
            }

            roll -= weight;
        }

        return options[^1].Value;
    }
}
