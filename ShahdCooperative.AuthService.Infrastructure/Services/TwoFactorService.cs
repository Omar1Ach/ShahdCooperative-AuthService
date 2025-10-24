using OtpNet;
using QRCoder;
using ShahdCooperative.AuthService.Domain.Interfaces;
using System.Security.Cryptography;

namespace ShahdCooperative.AuthService.Infrastructure.Services;

public class TwoFactorService : ITwoFactorService
{
    private const int BackupCodeLength = 8;
    private readonly IPasswordHasher _passwordHasher;

    public TwoFactorService(IPasswordHasher passwordHasher)
    {
        _passwordHasher = passwordHasher;
    }

    public string GenerateSecret()
    {
        // Generate a 20-byte (160-bit) secret key
        var key = KeyGeneration.GenerateRandomKey(20);

        // Encode as Base32 for compatibility with authenticator apps
        return Base32Encoding.ToString(key);
    }

    public string GenerateQrCodeUri(string email, string secret, string issuer = "ShahdCooperative")
    {
        // Create the otpauth:// URI format
        // Format: otpauth://totp/ISSUER:EMAIL?secret=SECRET&issuer=ISSUER
        var encodedIssuer = Uri.EscapeDataString(issuer);
        var encodedEmail = Uri.EscapeDataString(email);
        var encodedSecret = Uri.EscapeDataString(secret);

        return $"otpauth://totp/{encodedIssuer}:{encodedEmail}?secret={encodedSecret}&issuer={encodedIssuer}";
    }

    public string GenerateQrCodeImage(string qrCodeUri)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(qrCodeUri, QRCodeGenerator.ECCLevel.Q);
        using var qrCode = new PngByteQRCode(qrCodeData);

        var qrCodeBytes = qrCode.GetGraphic(20);
        return Convert.ToBase64String(qrCodeBytes);
    }

    public bool VerifyCode(string secret, string code, int timeStepWindow = 1)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        try
        {
            var secretBytes = Base32Encoding.ToBytes(secret);
            var totp = new Totp(secretBytes);

            // Verify with time window (default ±30 seconds)
            long timeStepMatched;
            return totp.VerifyTotp(code, out timeStepMatched, new VerificationWindow(timeStepWindow, timeStepWindow));
        }
        catch
        {
            return false;
        }
    }

    public List<string> GenerateBackupCodes(int count = 10)
    {
        var codes = new List<string>();

        for (int i = 0; i < count; i++)
        {
            codes.Add(GenerateBackupCode());
        }

        return codes;
    }

    public string HashBackupCode(string code)
    {
        // Use the existing password hasher for consistency
        var hash = _passwordHasher.HashPassword(code, out _);
        return hash;
    }

    public bool VerifyBackupCode(string code, string hash)
    {
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(hash))
        {
            return false;
        }

        return _passwordHasher.VerifyPassword(code, hash, string.Empty);
    }

    private string GenerateBackupCode()
    {
        // Generate a random 8-character alphanumeric code
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // Excluding similar-looking characters
        var bytes = RandomNumberGenerator.GetBytes(BackupCodeLength);

        var result = new char[BackupCodeLength];
        for (int i = 0; i < BackupCodeLength; i++)
        {
            result[i] = chars[bytes[i] % chars.Length];
        }

        return new string(result);
    }
}
