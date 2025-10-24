namespace ShahdCooperative.AuthService.Application.DTOs;

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? CaptchaToken { get; set; }
}
