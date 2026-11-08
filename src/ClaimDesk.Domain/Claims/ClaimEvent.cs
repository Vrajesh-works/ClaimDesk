namespace ClaimDesk.Domain.Claims;

/// <summary>
/// Append-only audit record. Created only through <see cref="Claim"/> methods; the
/// persistence layer rejects updates and deletes.
/// </summary>
public sealed class ClaimEvent
{
    public long Id { get; private set; }
    public Guid ClaimId { get; private set; }
    public ClaimEventType Type { get; private set; }
    public ClaimStatus? FromStatus { get; private set; }
    public ClaimStatus? ToStatus { get; private set; }
    public Guid? ActorId { get; private set; }
    public string ActorRole { get; private set; } = string.Empty;
    public string? Note { get; private set; }
    public DateTime OccurredAt { get; private set; }

    private ClaimEvent()
    {
    }

    internal static ClaimEvent Create(
        Guid claimId,
        ClaimEventType type,
        ClaimStatus? from,
        ClaimStatus? to,
        Guid? actorId,
        string actorRole,
        string? note,
        DateTime occurredAt) => new()
        {
            ClaimId = claimId,
            Type = type,
            FromStatus = from,
            ToStatus = to,
            ActorId = actorId,
            ActorRole = actorRole,
            Note = note,
            OccurredAt = occurredAt,
        };
}
