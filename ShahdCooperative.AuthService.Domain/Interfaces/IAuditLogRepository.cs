using ShahdCooperative.AuthService.Domain.Entities;

namespace ShahdCooperative.AuthService.Domain.Interfaces;

public interface IAuditLogRepository
{
    Task<Guid> CreateAsync(AuditLog auditLog);
    Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, int limit = 50);
    Task<IEnumerable<AuditLog>> GetRecentAsync(int limit = 100);
    Task<(List<AuditLog> Logs, int TotalCount)> GetAllAsync(int pageNumber, int pageSize, Guid? userId, string? action, DateTime? startDate, DateTime? endDate);
}
