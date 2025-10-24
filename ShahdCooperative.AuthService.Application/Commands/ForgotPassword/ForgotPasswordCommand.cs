using MediatR;

namespace ShahdCooperative.AuthService.Application.Commands.ForgotPassword;

public record ForgotPasswordCommand(string Email) : IRequest<bool>;
