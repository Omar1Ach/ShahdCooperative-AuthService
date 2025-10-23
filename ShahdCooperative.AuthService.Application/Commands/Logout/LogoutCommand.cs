using MediatR;

namespace ShahdCooperative.AuthService.Application.Commands.Logout;

public record LogoutCommand(string RefreshToken) : IRequest<bool>;
