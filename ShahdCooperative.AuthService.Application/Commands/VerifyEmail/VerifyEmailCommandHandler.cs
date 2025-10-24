using MediatR;
using ShahdCooperative.AuthService.Domain.Exceptions;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Application.Commands.VerifyEmail;

public class VerifyEmailCommandHandler : IRequestHandler<VerifyEmailCommand, bool>
{
    private readonly IUserRepository _userRepository;

    public VerifyEmailCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> Handle(VerifyEmailCommand request, CancellationToken cancellationToken)
    {
        // Get user by verification token
        var user = await _userRepository.GetByEmailVerificationTokenAsync(request.Token);

        if (user == null)
        {
            throw new TokenExpiredException("Invalid or expired verification token.");
        }

        // Check if token is expired
        if (user.EmailVerificationExpiry == null || user.EmailVerificationExpiry < DateTime.UtcNow)
        {
            throw new TokenExpiredException("Verification token has expired.");
        }

        // Check if already verified
        if (user.IsEmailVerified)
        {
            return true; // Already verified, return success
        }

        // Verify email
        await _userRepository.VerifyEmailAsync(user.Id);

        return true;
    }
}
