using MediatR;
using ShahdCooperative.AuthService.Domain.Exceptions;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Application.Commands.ChangePassword;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IEmailService _emailService;

    public ChangePasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IEmailService emailService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _emailService = emailService;
    }

    public async Task<bool> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        // Get user
        var user = await _userRepository.GetByIdAsync(request.UserId);

        if (user == null || !user.IsActive)
        {
            throw new UserNotFoundException("User not found or inactive.");
        }

        // Verify current password
        var isValid = _passwordHasher.VerifyPassword(request.CurrentPassword, user.PasswordHash, user.PasswordSalt);

        if (!isValid)
        {
            throw new InvalidCredentialsException("Current password is incorrect.");
        }

        // Hash new password
        string salt;
        var passwordHash = _passwordHasher.HashPassword(request.NewPassword, out salt);

        // Update password
        await _userRepository.UpdatePasswordAsync(user.Id, passwordHash, salt);

        // Send notification email
        await _emailService.SendPasswordChangedNotificationAsync(user.Email);

        return true;
    }
}
