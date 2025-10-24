namespace ShahdCooperative.AuthService.Application.DTOs;

public class LoginResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = new();
    public bool Requires2FA { get; set; } = false;
    public string Message { get; set; } = string.Empty;
}
