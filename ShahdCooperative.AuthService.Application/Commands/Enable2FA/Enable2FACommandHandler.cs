using MediatR;
using ShahdCooperative.AuthService.Application.DTOs;
using ShahdCooperative.AuthService.Domain.Interfaces;
using System.Text.Json;

namespace ShahdCooperative.AuthService.Application.Commands.Enable2FA;

public class Enable2FACommandHandler : IRequestHandler<Enable2FACommand, Enable2FAResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly ITwoFactorService _twoFactorService;

    public Enable2FACommandHandler(
        IUserRepository userRepository,
        ITwoFactorService twoFactorService)
    {
        _userRepository = userRepository;
        _twoFactorService = twoFactorService;
    }

    public async Task<Enable2FAResponse> Handle(Enable2FACommand request, CancellationToken cancellationToken)
    {
        // Get user
        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (!user.IsActive)
        {
            throw new InvalidOperationException("User account is not active");
        }

        if (user.TwoFactorEnabled)
        {
            throw new InvalidOperationException("Two-factor authentication is already enabled for this user");
        }

        // Generate secret key
        var secret = _twoFactorService.GenerateSecret();

        // Generate QR code
        var qrCodeUri = _twoFactorService.GenerateQrCodeUri(user.Email, secret);
        var qrCodeImage = _twoFactorService.GenerateQrCodeImage(qrCodeUri);

        // Generate backup codes
        var backupCodes = _twoFactorService.GenerateBackupCodes(10);

        // Hash backup codes for storage
        var hashedBackupCodes = backupCodes.Select(code => _twoFactorService.HashBackupCode(code)).ToList();

        // Store secret and hashed backup codes (but don't enable 2FA yet - wait for verification)
        user.TwoFactorSecret = secret;
        user.BackupCodes = JsonSerializer.Serialize(hashedBackupCodes);
        user.TwoFactorEnabled = false; // Will be enabled after verification

        await _userRepository.UpdateAsync(user);

        return new Enable2FAResponse
        {
            Secret = secret,
            QrCodeImage = qrCodeImage,
            BackupCodes = backupCodes,
            Message = "2FA has been configured. Scan the QR code with your authenticator app and verify with a code to complete setup."
        };
    }
}
