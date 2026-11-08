namespace ClaimDesk.Application.Abstractions;

/// <summary>
/// Binary storage for claim evidence. The local implementation writes to disk and issues
/// HMAC-signed links; an Azure implementation would write to Blob Storage and issue SAS URLs.
/// </summary>
public interface IEvidenceStore
{
    Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken);

    Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken);

    /// <summary>Creates a time-limited read URL for the stored object.</summary>
    Uri CreateReadUrl(string key, string fileName, string contentType, TimeSpan lifetime);
}
