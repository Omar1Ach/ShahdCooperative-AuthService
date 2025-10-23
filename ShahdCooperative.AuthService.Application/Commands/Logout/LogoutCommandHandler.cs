using MediatR;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Application.Commands.Logout;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, bool>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuditLogRepository _auditLogRepository;

    public LogoutCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IAuditLogRepository auditLogRepository)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _auditLogRepository = auditLogRepository;
    }

    public async Task<bool> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // Get refresh token
        var refreshToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken);

        if (refreshToken == null)
        {
            return false;
        }

        // Revoke the refresh token
        await _refreshTokenRepository.RevokeAsync(request.RefreshToken);

        // Log audit
        await _auditLogRepository.CreateAsync(new Domain.Entities.AuditLog
        {
            UserId = refreshToken.UserId,
            Action = "Logout",
            Success = true,
            CreatedAt = DateTime.UtcNow
        });

        return true;
    }
}
