namespace ShahdCooperative.AuthService.Domain.Interfaces;

public interface ITwoFactorService
{
    /// <summary>
    /// Generates a new secret key for TOTP authentication
    /// </summary>
    /// <returns>Base32 encoded secret key</returns>
    string GenerateSecret();

    /// <summary>
    /// Generates a QR code URI for authenticator apps
    /// </summary>
    /// <param name="email">User's email address</param>
    /// <param name="secret">TOTP secret key</param>
    /// <param name="issuer">Application name (e.g., "ShahdCooperative")</param>
    /// <returns>otpauth:// URI for QR code generation</returns>
    string GenerateQrCodeUri(string email, string secret, string issuer = "ShahdCooperative");

    /// <summary>
    /// Generates a QR code image as base64 string
    /// </summary>
    /// <param name="qrCodeUri">The otpauth:// URI</param>
    /// <returns>Base64 encoded PNG image</returns>
    string GenerateQrCodeImage(string qrCodeUri);

    /// <summary>
    /// Verifies a TOTP code against a secret
    /// </summary>
    /// <param name="secret">The TOTP secret key</param>
    /// <param name="code">The 6-digit code to verify</param>
    /// <param name="timeStepWindow">Number of time steps to check (default: 1 = ±30 seconds)</param>
    /// <returns>True if code is valid</returns>
    bool VerifyCode(string secret, string code, int timeStepWindow = 1);

    /// <summary>
    /// Generates backup recovery codes
    /// </summary>
    /// <param name="count">Number of backup codes to generate (default: 10)</param>
    /// <returns>List of backup codes (plaintext)</returns>
    List<string> GenerateBackupCodes(int count = 10);

    /// <summary>
    /// Hashes a backup code for secure storage
    /// </summary>
    /// <param name="code">The plaintext backup code</param>
    /// <returns>Hashed backup code</returns>
    string HashBackupCode(string code);

    /// <summary>
    /// Verifies a backup code against its hash
    /// </summary>
    /// <param name="code">The plaintext backup code to verify</param>
    /// <param name="hash">The hashed backup code</param>
    /// <returns>True if code matches the hash</returns>
    bool VerifyBackupCode(string code, string hash);
}
