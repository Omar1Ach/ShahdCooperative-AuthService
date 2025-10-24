using MediatR;
using ShahdCooperative.AuthService.Application.DTOs;

namespace ShahdCooperative.AuthService.Application.Commands.Verify2FACode;

public record Verify2FACodeCommand(
    string Email,
    string Code,
    bool UseBackupCode,
    string? IpAddress,
    string? UserAgent
) : IRequest<LoginResponse>;
