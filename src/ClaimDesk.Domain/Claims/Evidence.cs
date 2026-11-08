namespace ClaimDesk.Domain.Claims;

public sealed class Evidence
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ClaimId { get; set; }
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long SizeBytes { get; set; }
    public required string StorageKey { get; set; }
    public required string Sha256 { get; set; }
    public Guid UploadedById { get; set; }
    public DateTime UploadedAt { get; set; }
}
