namespace ShahdCooperative.AuthService.Application.DTOs;

public class Disable2FARequest
{
    public Guid UserId { get; set; }
    public string Password { get; set; } = string.Empty;
}
