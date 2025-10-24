using MediatR;

namespace ShahdCooperative.AuthService.Application.Commands.Verify2FASetup;

public record Verify2FASetupCommand(Guid UserId, string Code) : IRequest<bool>;
