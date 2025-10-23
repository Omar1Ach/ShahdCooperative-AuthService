using MediatR;
using ShahdCooperative.AuthService.Application.DTOs;

namespace ShahdCooperative.AuthService.Application.Commands.Login;

public record LoginCommand(string Email, string Password, string? IpAddress = null, string? UserAgent = null) : IRequest<LoginResponse>;
