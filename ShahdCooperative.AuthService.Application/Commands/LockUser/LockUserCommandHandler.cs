using MediatR;
using ShahdCooperative.AuthService.Domain.Exceptions;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Application.Commands.LockUser;

public class LockUserCommandHandler : IRequestHandler<LockUserCommand, bool>
{
    private readonly IUserRepository _userRepository;

    public LockUserCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> Handle(LockUserCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);

        if (user == null)
        {
            throw new UserNotFoundException("User not found.");
        }

        var lockoutEnd = DateTime.UtcNow.AddMinutes(request.LockoutMinutes);
        await _userRepository.SetLockoutEndAsync(request.UserId, lockoutEnd);

        return true;
    }
}
