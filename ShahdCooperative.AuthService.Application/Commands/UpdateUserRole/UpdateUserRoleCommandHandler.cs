using MediatR;
using ShahdCooperative.AuthService.Domain.Exceptions;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Application.Commands.UpdateUserRole;

public class UpdateUserRoleCommandHandler : IRequestHandler<UpdateUserRoleCommand, bool>
{
    private readonly IUserRepository _userRepository;

    public UpdateUserRoleCommandHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<bool> Handle(UpdateUserRoleCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync(request.UserId);

        if (user == null)
        {
            throw new UserNotFoundException("User not found.");
        }

        await _userRepository.UpdateRoleAsync(request.UserId, request.Role);

        return true;
    }
}
