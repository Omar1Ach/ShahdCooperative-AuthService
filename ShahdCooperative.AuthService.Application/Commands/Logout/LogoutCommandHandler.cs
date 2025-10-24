using MediatR;
using ShahdCooperative.AuthService.Domain.Events;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Application.Commands.Logout;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, bool>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IMessagePublisher _messagePublisher;

    public LogoutCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IAuditLogRepository auditLogRepository,
        IMessagePublisher messagePublisher)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _auditLogRepository = auditLogRepository;
        _messagePublisher = messagePublisher;
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

        // Publish event to RabbitMQ
        try
        {
            await _messagePublisher.PublishAsync(new UserLoggedOutEvent
            {
                UserId = refreshToken.UserId,
                LoggedOutAt = DateTime.UtcNow
            }, "user.logged-out");
        }
        catch (Exception)
        {
            // Log error but don't fail the request
        }

        return true;
    }
}
