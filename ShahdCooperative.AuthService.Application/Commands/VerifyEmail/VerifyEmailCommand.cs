using MediatR;

namespace ShahdCooperative.AuthService.Application.Commands.VerifyEmail;

public record VerifyEmailCommand(string Token) : IRequest<bool>;
