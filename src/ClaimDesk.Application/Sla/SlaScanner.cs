using ClaimDesk.Application.Abstractions;
using ClaimDesk.Domain.Claims;
using ClaimDesk.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ClaimDesk.Application.Sla;

public sealed record SlaScanResult(int Reminders, int StuckAlerts);

/// <summary>
/// Finds claims that breach the pickup SLA or have stalled, records an audit event on each,
/// and raises a notification. Idempotent within <see cref="SlaOptions.RepeatEvery"/>.
/// </summary>
public sealed class SlaScanner(
    IClaimDeskDbContext db,
    IOptions<SlaOptions> options,
    INotificationSink sink,
    TimeProvider time)
{
    public async Task<SlaScanResult> ScanAsync(CancellationToken ct)
    {
        var sla = options.Value;
        var now = time.GetUtcNow().UtcDateTime;
        var reminderCutoff = now - sla.SubmittedReminderAfter;
        var stuckCutoff = now - sla.StuckAfter;
        var repeatCutoff = now - sla.RepeatEvery;

        var overdue = await db.Claims
            .Where(c => c.Status == ClaimStatus.Submitted
                        && c.SubmittedAt <= reminderCutoff
                        && (c.LastSlaReminderAt == null || c.LastSlaReminderAt <= repeatCutoff))
            .ToListAsync(ct);

        var stuck = await db.Claims
            .Where(c => (c.Status == ClaimStatus.Submitted
                         || c.Status == ClaimStatus.InReview
                         || c.Status == ClaimStatus.MoreInfoNeeded)
                        && c.LastActivityAt <= stuckCutoff
                        && (c.LastStuckAlertAt == null || c.LastStuckAlertAt <= repeatCutoff))
            .ToListAsync(ct);

        var notifications = new List<Notification>();

        foreach (var claim in overdue)
        {
            var waited = now - claim.SubmittedAt;
            var message = $"Claim {claim.Number} has waited {FormatSpan(waited)} for an adjuster (SLA {FormatSpan(sla.SubmittedReminderAfter)}).";
            claim.RecordSlaReminder(message, now);
            notifications.Add(NewNotification(claim, NotificationKind.SlaReminder, Roles.Adjuster, message, now));
        }

        foreach (var claim in stuck)
        {
            var idle = now - claim.LastActivityAt;
            var message = $"Claim {claim.Number} is stuck in {claim.Status} with no activity for {FormatSpan(idle)}.";
            claim.RecordStuckAlert(message, now);
            notifications.Add(NewNotification(claim, NotificationKind.StuckClaim, Roles.Admin, message, now));
        }

        if (notifications.Count == 0)
        {
            return new SlaScanResult(0, 0);
        }

        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(ct);

        foreach (var notification in notifications)
        {
            await sink.PublishAsync(notification, ct);
        }

        ClaimDeskMetrics.SlaAlerts.Add(notifications.Count);
        return new SlaScanResult(overdue.Count, stuck.Count);
    }

    private static Notification NewNotification(Claim claim, NotificationKind kind, string role, string message, DateTime now) => new()
    {
        ClaimId = claim.Id,
        Kind = kind,
        RecipientRole = role,
        Message = message,
        CreatedAt = now,
    };

    private static string FormatSpan(TimeSpan span)
    {
        var (value, unit) = span.TotalDays >= 1 ? (span.TotalDays, "day") : (span.TotalHours, "hour");
        var text = value.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        return text == "1" ? $"1 {unit}" : $"{text} {unit}s";
    }
}
