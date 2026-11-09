using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace ClaimDesk.Infrastructure.Storage;

public sealed record SignedLinkPayload(string Key, string FileName, string ContentType, long ExpiresAtUnix);

/// <summary>
/// Issues and validates HMAC-SHA256 signed, expiring download tokens. This is the local
/// stand-in for an Azure Blob Storage SAS token: possession of the URL grants read access
/// to one object until it expires.
/// </summary>
public sealed class SignedLinkProtector
{
    private readonly byte[] _key;
    private readonly TimeProvider _time;

    public SignedLinkProtector(string signingKey, TimeProvider time)
    {
        if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
        {
            throw new ArgumentException("Evidence link signing key must be at least 32 characters.", nameof(signingKey));
        }

        _key = Encoding.UTF8.GetBytes(signingKey);
        _time = time;
    }

    public string Protect(string key, string fileName, string contentType, TimeSpan lifetime)
    {
        var expires = _time.GetUtcNow().Add(lifetime).ToUnixTimeSeconds();
        var payload = JsonSerializer.SerializeToUtf8Bytes(new SignedLinkPayload(key, fileName, contentType, expires));
        var signature = HMACSHA256.HashData(_key, payload);
        return $"{WebEncoders.Base64UrlEncode(payload)}.{WebEncoders.Base64UrlEncode(signature)}";
    }

    public SignedLinkPayload? Unprotect(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var parts = token.Split('.');
        if (parts.Length != 2)
        {
            return null;
        }

        try
        {
            var payload = WebEncoders.Base64UrlDecode(parts[0]);
            var signature = WebEncoders.Base64UrlDecode(parts[1]);
            var expected = HMACSHA256.HashData(_key, payload);
            if (!CryptographicOperations.FixedTimeEquals(signature, expected))
            {
                return null;
            }

            var data = JsonSerializer.Deserialize<SignedLinkPayload>(payload);
            if (data is null || data.ExpiresAtUnix < _time.GetUtcNow().ToUnixTimeSeconds())
            {
                return null;
            }

            return data;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
