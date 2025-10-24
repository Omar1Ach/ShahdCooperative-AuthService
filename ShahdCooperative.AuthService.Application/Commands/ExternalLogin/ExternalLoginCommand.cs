using MediatR;
using ShahdCooperative.AuthService.Application.DTOs;

namespace ShahdCooperative.AuthService.Application.Commands.ExternalLogin;

public record ExternalLoginCommand(
    string Provider,
    string ProviderKey,
    string Email,
    string? ProviderDisplayName,
    string? IpAddress,
    string? UserAgent
) : IRequest<LoginResponse>;
