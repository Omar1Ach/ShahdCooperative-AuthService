using MediatR;

namespace ShahdCooperative.AuthService.Application.Commands.UnlockUser;

public record UnlockUserCommand(Guid UserId) : IRequest<bool>;
