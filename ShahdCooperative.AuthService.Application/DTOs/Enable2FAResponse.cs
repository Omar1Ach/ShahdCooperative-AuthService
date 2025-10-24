namespace ShahdCooperative.AuthService.Application.DTOs;

public class Enable2FAResponse
{
    public string Secret { get; set; } = string.Empty;
    public string QrCodeImage { get; set; } = string.Empty; // Base64 encoded PNG
    public List<string> BackupCodes { get; set; } = new();
    public string Message { get; set; } = "2FA has been configured. Scan the QR code with your authenticator app and verify with a code.";
}
