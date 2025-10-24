namespace ShahdCooperative.AuthService.Application.DTOs;

public class SecurityDashboardDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int LockedUsers { get; set; }
    public int UnverifiedEmails { get; set; }
    public int TotalAuditLogs { get; set; }
    public int FailedLoginsToday { get; set; }
    public int SuccessfulLoginsToday { get; set; }
    public List<AuditLogDto> RecentActivities { get; set; } = new();
}
