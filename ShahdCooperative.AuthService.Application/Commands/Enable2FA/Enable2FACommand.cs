using MediatR;
using ShahdCooperative.AuthService.Application.DTOs;

namespace ShahdCooperative.AuthService.Application.Commands.Enable2FA;

public record Enable2FACommand(Guid UserId) : IRequest<Enable2FAResponse>;
