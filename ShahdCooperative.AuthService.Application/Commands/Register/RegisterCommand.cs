using MediatR;
using ShahdCooperative.AuthService.Application.DTOs;

namespace ShahdCooperative.AuthService.Application.Commands.Register;

public record RegisterCommand(string Email, string Password) : IRequest<LoginResponse>;
