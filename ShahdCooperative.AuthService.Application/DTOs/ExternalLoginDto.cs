namespace ShahdCooperative.AuthService.Application.DTOs;

public class ExternalLoginDto
{
    public string Provider { get; set; } = string.Empty;
    public string? ProviderDisplayName { get; set; }
    public string? Email { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
