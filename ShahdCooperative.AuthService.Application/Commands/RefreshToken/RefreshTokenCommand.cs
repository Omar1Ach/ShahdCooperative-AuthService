using MediatR;
using ShahdCooperative.AuthService.Application.DTOs;

namespace ShahdCooperative.AuthService.Application.Commands.RefreshToken;

public record RefreshTokenCommand(string RefreshToken) : IRequest<LoginResponse>;
