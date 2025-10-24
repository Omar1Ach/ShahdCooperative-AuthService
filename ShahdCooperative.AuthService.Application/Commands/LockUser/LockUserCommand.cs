using MediatR;

namespace ShahdCooperative.AuthService.Application.Commands.LockUser;

public record LockUserCommand(Guid UserId, int LockoutMinutes = 1440) : IRequest<bool>; // Default 24 hours
