using AutoMapper;
using MediatR;
using ShahdCooperative.AuthService.Application.DTOs;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Application.Queries.GetSecurityDashboard;

public class GetSecurityDashboardQueryHandler : IRequestHandler<GetSecurityDashboardQuery, SecurityDashboardDto>
{
    private readonly IUserRepository _userRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IMapper _mapper;

    public GetSecurityDashboardQueryHandler(IUserRepository userRepository, IAuditLogRepository auditLogRepository, IMapper mapper)
    {
        _userRepository = userRepository;
        _auditLogRepository = auditLogRepository;
        _mapper = mapper;
    }

    public async Task<SecurityDashboardDto> Handle(GetSecurityDashboardQuery request, CancellationToken cancellationToken)
    {
        // Get all users to calculate stats (in production, this should be optimized with specific queries)
        var (allUsers, totalUsers) = await _userRepository.GetAllAsync(1, int.MaxValue, null, null, false);

        var activeUsers = allUsers.Count(u => u.IsActive);
        var lockedUsers = allUsers.Count(u => u.LockoutEnd.HasValue && u.LockoutEnd > DateTime.UtcNow);
        var unverifiedEmails = allUsers.Count(u => !u.IsEmailVerified);

        // Get recent audit logs
        var recentLogs = await _auditLogRepository.GetRecentAsync(10);
        var recentLogDtos = _mapper.Map<List<AuditLogDto>>(recentLogs);

        // Get today's login stats
        var startOfDay = DateTime.UtcNow.Date;
        var (todayLogs, _) = await _auditLogRepository.GetAllAsync(1, int.MaxValue, null, "Login", startOfDay, null);

        var failedLoginsToday = todayLogs.Count(l => l.Result == "Failed");
        var successfulLoginsToday = todayLogs.Count(l => l.Result == "Success");

        return new SecurityDashboardDto
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            LockedUsers = lockedUsers,
            UnverifiedEmails = unverifiedEmails,
            FailedLoginsToday = failedLoginsToday,
            SuccessfulLoginsToday = successfulLoginsToday,
            RecentActivities = recentLogDtos
        };
    }
}
