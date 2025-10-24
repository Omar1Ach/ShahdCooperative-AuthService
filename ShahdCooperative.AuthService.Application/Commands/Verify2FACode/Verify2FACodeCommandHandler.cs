using MediatR;
using ShahdCooperative.AuthService.Application.DTOs;
using ShahdCooperative.AuthService.Domain.Interfaces;
using System.Text.Json;

namespace ShahdCooperative.AuthService.Application.Commands.Verify2FACode;

public class Verify2FACodeCommandHandler : IRequestHandler<Verify2FACodeCommand, LoginResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly ITwoFactorService _twoFactorService;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public Verify2FACodeCommandHandler(
        IUserRepository userRepository,
        ITwoFactorService twoFactorService,
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IAuditLogRepository auditLogRepository)
    {
        _userRepository = userRepository;
        _twoFactorService = twoFactorService;
        _tokenService = tokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<LoginResponse> Handle(Verify2FACodeCommand request, CancellationToken cancellationToken)
    {
        // Get user
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null || !user.TwoFactorEnabled)
        {
            throw new UnauthorizedAccessException("Invalid 2FA verification request");
        }

        bool isValid = false;

        if (request.UseBackupCode)
        {
            // Verify backup code
            if (string.IsNullOrWhiteSpace(user.BackupCodes))
            {
                throw new UnauthorizedAccessException("No backup codes available");
            }

            var backupCodes = JsonSerializer.Deserialize<List<string>>(user.BackupCodes) ?? new List<string>();

            // Check if code matches any backup code
            var matchedCode = backupCodes.FirstOrDefault(hash =>
                _twoFactorService.VerifyBackupCode(request.Code, hash));

            if (matchedCode != null)
            {
                isValid = true;

                // Remove used backup code
                backupCodes.Remove(matchedCode);
                user.BackupCodes = JsonSerializer.Serialize(backupCodes);
                await _userRepository.UpdateAsync(user);
            }
        }
        else
        {
            // Verify TOTP code
            if (string.IsNullOrWhiteSpace(user.TwoFactorSecret))
            {
                throw new UnauthorizedAccessException("2FA secret not configured");
            }

            isValid = _twoFactorService.VerifyCode(user.TwoFactorSecret, request.Code);
        }

        if (!isValid)
        {
            // Log failed 2FA attempt
            await _auditLogRepository.CreateAsync(new Domain.Entities.AuditLog
            {
                UserId = user.Id,
                Action = "2FA Verification Failed",
                Result = "Failed",
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                Details = $"Failed 2FA verification attempt for user {user.Email}",
                CreatedAt = DateTime.UtcNow
            });

            throw new UnauthorizedAccessException("Invalid 2FA code");
        }

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        var expiresAt = DateTime.UtcNow.AddMinutes(15); // Token expiry

        // Store refresh token
        await _refreshTokenRepository.CreateAsync(new Domain.Entities.RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            CreatedAt = DateTime.UtcNow
        });

        // Update last login
        await _userRepository.UpdateLastLoginAsync(user.Id);

        // Log successful 2FA login
        await _auditLogRepository.CreateAsync(new Domain.Entities.AuditLog
        {
            UserId = user.Id,
            Action = "2FA Login Success",
            Result = "Success",
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            Details = $"User {user.Email} logged in successfully with 2FA",
            CreatedAt = DateTime.UtcNow
        });

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = new UserDto
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,
                IsEmailVerified = user.IsEmailVerified
            },
            Requires2FA = false,
            Message = "Login successful"
        };
    }
}
