using System.Diagnostics.Metrics;

namespace ClaimDesk.Application;

/// <summary>Custom OpenTelemetry metrics (exported to the Aspire dashboard locally, Azure Monitor later).</summary>
public static class ClaimDeskMetrics
{
    public const string MeterName = "ClaimDesk";

    private static readonly Meter Meter = new(MeterName);

    public static readonly Counter<long> ClaimsFiled = Meter.CreateCounter<long>("claimdesk.claims.filed");
    public static readonly Counter<long> Transitions = Meter.CreateCounter<long>("claimdesk.claims.transitions");
    public static readonly Counter<long> ClaimsFlagged = Meter.CreateCounter<long>("claimdesk.claims.flagged");
    public static readonly Counter<long> EvidenceUploaded = Meter.CreateCounter<long>("claimdesk.evidence.uploaded");
    public static readonly Counter<long> SlaAlerts = Meter.CreateCounter<long>("claimdesk.sla.alerts");
}
