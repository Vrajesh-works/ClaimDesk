namespace ClaimDesk.Application.Sla;

public sealed class SlaOptions
{
    public const string Section = "Sla";

    /// <summary>A submitted claim nobody has picked up after this long gets a reminder.</summary>
    public TimeSpan SubmittedReminderAfter { get; set; } = TimeSpan.FromHours(24);

    /// <summary>An open claim with no activity for this long raises a stuck-claim alert.</summary>
    public TimeSpan StuckAfter { get; set; } = TimeSpan.FromDays(5);

    /// <summary>Minimum gap before the same reminder or alert is raised again for a claim.</summary>
    public TimeSpan RepeatEvery { get; set; } = TimeSpan.FromHours(24);

    /// <summary>How often the background worker scans.</summary>
    public TimeSpan ScanInterval { get; set; } = TimeSpan.FromMinutes(5);
}
