namespace ClaimDesk.Domain.Users;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>Stored lower-case; lookups normalize input the same way.</summary>
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string Role { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
}
