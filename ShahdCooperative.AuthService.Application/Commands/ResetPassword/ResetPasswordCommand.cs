using MediatR;

namespace ShahdCooperative.AuthService.Application.Commands.ResetPassword;

public record ResetPasswordCommand(string Token, string NewPassword) : IRequest<bool>;
