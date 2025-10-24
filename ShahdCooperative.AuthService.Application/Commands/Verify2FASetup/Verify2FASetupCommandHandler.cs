using MediatR;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Application.Commands.Verify2FASetup;

public class Verify2FASetupCommandHandler : IRequestHandler<Verify2FASetupCommand, bool>
{
    private readonly IUserRepository _userRepository;
    private readonly ITwoFactorService _twoFactorService;

    public Verify2FASetupCommandHandler(
        IUserRepository userRepository,
        ITwoFactorService twoFactorService)
    {
        _userRepository = userRepository;
        _twoFactorService = twoFactorService;
    }

    public async Task<bool> Handle(Verify2FASetupCommand request, CancellationToken cancellationToken)
    {
        // Get user
        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (string.IsNullOrWhiteSpace(user.TwoFactorSecret))
        {
            throw new InvalidOperationException("2FA setup has not been initiated. Please enable 2FA first.");
        }

        if (user.TwoFactorEnabled)
        {
            throw new InvalidOperationException("Two-factor authentication is already enabled");
        }

        // Verify the code
        var isValid = _twoFactorService.VerifyCode(user.TwoFactorSecret, request.Code);

        if (!isValid)
        {
            return false;
        }

        // Enable 2FA
        user.TwoFactorEnabled = true;
        await _userRepository.UpdateAsync(user);

        return true;
    }
}
