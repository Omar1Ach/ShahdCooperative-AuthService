using MediatR;
using ShahdCooperative.AuthService.Domain.Exceptions;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Application.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;

    public ResetPasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IPasswordHasher passwordHasher,
        IEmailService emailService)
    {
        _userRepository = userRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
    }

    public async Task<bool> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        // Get token
        var resetToken = await _passwordResetTokenRepository.GetByTokenAsync(request.Token);

        if (resetToken == null || !resetToken.IsValid)
        {
            throw new TokenExpiredException("Invalid or expired reset token.");
        }

        // Get user
        var user = await _userRepository.GetByIdAsync(resetToken.UserId);

        if (user == null || !user.IsActive)
        {
            throw new UserNotFoundException("User not found or inactive.");
        }

        // Hash new password
        string salt;
        var passwordHash = _passwordHasher.HashPassword(request.NewPassword, out salt);

        // Update password
        await _userRepository.UpdatePasswordAsync(user.Id, passwordHash, salt);

        // Mark token as used
        await _passwordResetTokenRepository.MarkAsUsedAsync(resetToken.Id);

        // Send notification email
        await _emailService.SendPasswordChangedNotificationAsync(user.Email);

        return true;
    }
}
