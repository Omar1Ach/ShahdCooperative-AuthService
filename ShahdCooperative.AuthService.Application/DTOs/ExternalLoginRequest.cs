namespace ShahdCooperative.AuthService.Application.DTOs;

public class ExternalLoginRequest
{
    public string Provider { get; set; } = string.Empty; // Google, Facebook, etc.
    public string ProviderKey { get; set; } = string.Empty; // User's ID from provider
    public string Email { get; set; } = string.Empty;
    public string? ProviderDisplayName { get; set; }
}
