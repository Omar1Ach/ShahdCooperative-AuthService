using MediatR;

namespace ShahdCooperative.AuthService.Application.Commands.Disable2FA;

public record Disable2FACommand(Guid UserId, string Password) : IRequest<bool>;
