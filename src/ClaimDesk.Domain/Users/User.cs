namespace ClaimDesk.Domain.Users;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string Role { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
}
