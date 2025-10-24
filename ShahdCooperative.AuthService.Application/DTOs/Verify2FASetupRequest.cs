namespace ShahdCooperative.AuthService.Application.DTOs;

public class Verify2FASetupRequest
{
    public Guid UserId { get; set; }
    public string Code { get; set; } = string.Empty;
}
