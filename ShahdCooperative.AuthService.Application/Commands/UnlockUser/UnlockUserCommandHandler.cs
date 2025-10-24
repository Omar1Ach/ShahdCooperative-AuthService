using MediatR;
using ShahdCooperative.AuthService.Domain.Exceptions;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Application.Commands.UnlockUser;

public class UnlockUserCommandHandler : IRequestHandler<UnlockUserCommand, bool>
{
    private readonly IUserRepository _userRepository;

    public UnlockUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> Handle(UnlockUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);

        if (user == null)
        {
            throw new UserNotFoundException("User not found.");
        }

        await _userRepository.ResetFailedLoginAttemptsAsync(request.UserId);

        return true;
    }
}
