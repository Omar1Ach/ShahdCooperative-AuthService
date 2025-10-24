using MediatR;

namespace ShahdCooperative.AuthService.Application.Commands.DeleteUser;

public record DeleteUserCommand(Guid UserId) : IRequest<bool>;
