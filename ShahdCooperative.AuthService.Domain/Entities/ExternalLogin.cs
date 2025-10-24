namespace ShahdCooperative.AuthService.Domain.Entities;

public class ExternalLogin
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Provider { get; set; } = string.Empty; // Google, Facebook, Microsoft, etc.
    public string ProviderKey { get; set; } = string.Empty; // User's ID from the provider
    public string? ProviderDisplayName { get; set; }
    public string? Email { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    // Navigation property
    public User? User { get; set; }
}
