using MediatR;

namespace ShahdCooperative.AuthService.Application.Commands.UpdateUserRole;

public record UpdateUserRoleCommand(Guid UserId, string Role) : IRequest<bool>;
