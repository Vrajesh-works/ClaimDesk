using ClaimDesk.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace ClaimDesk.Infrastructure.Storage;

public sealed class EvidenceStorageOptions
{
    public const string Section = "EvidenceStorage";

    /// <summary>Directory where evidence files are written.</summary>
    public string RootPath { get; set; } = "data/evidence";

    /// <summary>HMAC key for download links (the local equivalent of a storage account key).</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>Path of the download endpoint that redeems signed tokens.</summary>
    public string DownloadPath { get; set; } = "/api/evidence/download";
}

/// <summary>Local-disk <see cref="IEvidenceStore"/>. Swap for a Blob Storage adapter in Azure.</summary>
public sealed class LocalDiskEvidenceStore(IOptions<EvidenceStorageOptions> options, SignedLinkProtector protector) : IEvidenceStore
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task SaveAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var path = ResolvePath(key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, cancellationToken);
    }

    public Task<Stream?> OpenReadAsync(string key, CancellationToken cancellationToken)
    {
        var path = ResolvePath(key);
        Stream? stream = File.Exists(path)
            ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true)
            : null;
        return Task.FromResult(stream);
    }

    public Uri CreateReadUrl(string key, string fileName, string contentType, TimeSpan lifetime)
    {
        var token = protector.Protect(key, fileName, contentType, lifetime);
        return new Uri($"{options.Value.DownloadPath}?token={token}", UriKind.Relative);
    }

    /// <summary>Maps a storage key to a path, refusing anything that escapes the root directory.</summary>
    private string ResolvePath(string key)
    {
        var path = Path.GetFullPath(Path.Combine(_root, key));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Storage key resolves outside the evidence root.");
        }

        return path;
    }
}
