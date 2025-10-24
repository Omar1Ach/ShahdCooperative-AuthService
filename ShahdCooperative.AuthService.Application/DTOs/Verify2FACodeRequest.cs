namespace ShahdCooperative.AuthService.Application.DTOs;

public class Verify2FACodeRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public bool UseBackupCode { get; set; } = false;
}
