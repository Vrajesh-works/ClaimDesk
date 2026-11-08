namespace ClaimDesk.Domain.Claims;

public enum NotificationKind
{
    SlaReminder,
    StuckClaim,
}

/// <summary>In-app notification raised by the SLA worker for adjusters and admins.</summary>
public sealed class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClaimId { get; set; }
    public NotificationKind Kind { get; set; }
    public required string RecipientRole { get; set; }
    public required string Message { get; set; }
    public DateTime CreatedAt { get; set; }
}
