namespace Imockey.Domain;

public class UserModel
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = string.Empty;

    // Null for users that only sign in with GitHub.
    public string? UserName { get; set; }
    public string? PasswordHash { get; set; }

    // Null for users that only sign in with username and password.
    public string? GitHubId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
