using MediatR;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Application.Commands.Disable2FA;

public class Disable2FACommandHandler : IRequestHandler<Disable2FACommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    public Disable2FACommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public async Task<bool> Handle(Disable2FACommand request, CancellationToken cancellationToken)
    {
        // Get user
        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (!user.TwoFactorEnabled)
        {
            throw new InvalidOperationException("Two-factor authentication is not enabled");
        }

        // Verify password
        var isPasswordValid = _passwordHasher.VerifyPassword(
            request.Password,
            user.PasswordHash,
            user.PasswordSalt);

        if (!isPasswordValid)
        {
            throw new UnauthorizedAccessException("Invalid password");
        }

        // Disable 2FA and clear secrets
        user.TwoFactorEnabled = false;
        user.TwoFactorSecret = null;
        user.BackupCodes = null;

        await _userRepository.UpdateAsync(user);

        return true;
    }
}
