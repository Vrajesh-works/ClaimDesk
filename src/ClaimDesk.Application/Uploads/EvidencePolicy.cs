namespace ClaimDesk.Application.Uploads;

public sealed record EvidenceCheck(bool Ok, string? Error, string? ContentType)
{
    public static EvidenceCheck Fail(string error) => new(false, error, null);
}

/// <summary>
/// Validates uploads by size, extension and file signature (magic bytes). The declared
/// content type from the client is never trusted.
/// </summary>
public static class EvidencePolicy
{
    public const long MaxBytes = 10 * 1024 * 1024;

    private static readonly Dictionary<string, (string ContentType, byte[][] Signatures)> Allowed =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [".pdf"] = ("application/pdf", [[0x25, 0x50, 0x44, 0x46, 0x2D]]),
            [".png"] = ("image/png", [[0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]]),
            [".jpg"] = ("image/jpeg", [[0xFF, 0xD8, 0xFF]]),
            [".jpeg"] = ("image/jpeg", [[0xFF, 0xD8, 0xFF]]),
        };

    public static IReadOnlyCollection<string> AllowedExtensions => Allowed.Keys;

    public static EvidenceCheck Check(string fileName, long length, ReadOnlySpan<byte> header)
    {
        if (length <= 0)
        {
            return EvidenceCheck.Fail("File is empty.");
        }

        if (length > MaxBytes)
        {
            return EvidenceCheck.Fail($"File exceeds the {MaxBytes / (1024 * 1024)} MB limit.");
        }

        var extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension) || !Allowed.TryGetValue(extension, out var rule))
        {
            return EvidenceCheck.Fail("Only PDF, PNG and JPEG files are accepted.");
        }

        foreach (var signature in rule.Signatures)
        {
            if (header.Length >= signature.Length && header[..signature.Length].SequenceEqual(signature))
            {
                return new EvidenceCheck(true, null, rule.ContentType);
            }
        }

        return EvidenceCheck.Fail("File content does not match its extension.");
    }

    /// <summary>Strips any path and unsafe characters from a client-supplied file name.</summary>
    public static string SanitizeFileName(string fileName)
    {
        var name = Path.GetFileName(fileName.Replace('\\', '/'));
        var cleaned = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_' or ' ').ToArray()).Trim();
        if (cleaned.Length == 0)
        {
            cleaned = "evidence";
        }

        return cleaned.Length > 120 ? cleaned[^120..] : cleaned;
    }
}
