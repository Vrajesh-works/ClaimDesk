namespace ClaimDesk.Api.Auth;

public sealed class JwtOptions
{
    public const string Section = "Jwt";

    public string Issuer { get; set; } = "claimdesk-local";
    public string Audience { get; set; } = "claimdesk-api";

    /// <summary>Symmetric signing key for the local issuer. Entra ID replaces this with its published signing keys.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(8);
}
