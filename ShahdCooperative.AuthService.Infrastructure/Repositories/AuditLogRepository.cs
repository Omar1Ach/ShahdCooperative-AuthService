using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ShahdCooperative.AuthService.Domain.Entities;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Infrastructure.Repositories;

public class AuditLogRepository : IAuditLogRepository
{
    private readonly string _connectionString;

    public AuditLogRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException(nameof(configuration), "Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<Guid> CreateAsync(AuditLog auditLog)
    {
        const string sql = @"
            INSERT INTO Security.AuditLogs
            (Id, UserId, Action, Result, IpAddress, UserAgent, Details, CreatedAt)
            VALUES
            (@Id, @UserId, @Action, @Result, @IpAddress, @UserAgent, @Details, @CreatedAt)";

        auditLog.Id = Guid.NewGuid();
        auditLog.CreatedAt = DateTime.UtcNow;

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, auditLog);

        return auditLog.Id;
    }

    public async Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, int limit = 50)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, Action, Result, IpAddress, UserAgent, Details, CreatedAt
            FROM Security.AuditLogs
            WHERE UserId = @UserId
            ORDER BY CreatedAt DESC";

        using var connection = CreateConnection();
        return await connection.QueryAsync<AuditLog>(sql, new { UserId = userId, Limit = limit });
    }

    public async Task<IEnumerable<AuditLog>> GetRecentAsync(int limit = 100)
    {
        const string sql = @"
            SELECT TOP (@Limit) Id, UserId, Action, Result, IpAddress, UserAgent, Details, CreatedAt
            FROM Security.AuditLogs
            ORDER BY CreatedAt DESC";

        using var connection = CreateConnection();
        return await connection.QueryAsync<AuditLog>(sql, new { Limit = limit });
    }

    public async Task<(List<AuditLog> Logs, int TotalCount)> GetAllAsync(int pageNumber, int pageSize, Guid? userId, string? action, DateTime? startDate, DateTime? endDate)
    {
        var offset = (pageNumber - 1) * pageSize;
        var whereClauses = new List<string>();
        var parameters = new DynamicParameters();

        if (userId.HasValue)
        {
            whereClauses.Add("UserId = @UserId");
            parameters.Add("UserId", userId.Value);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            whereClauses.Add("Action = @Action");
            parameters.Add("Action", action);
        }

        if (startDate.HasValue)
        {
            whereClauses.Add("CreatedAt >= @StartDate");
            parameters.Add("StartDate", startDate.Value);
        }

        if (endDate.HasValue)
        {
            whereClauses.Add("CreatedAt <= @EndDate");
            parameters.Add("EndDate", endDate.Value);
        }

        var whereClause = whereClauses.Any() ? "WHERE " + string.Join(" AND ", whereClauses) : "";

        parameters.Add("Offset", offset);
        parameters.Add("PageSize", pageSize);

        var sql = $@"
            SELECT Id, UserId, Action, Result, IpAddress, UserAgent, Details, CreatedAt
            FROM Security.AuditLogs
            {whereClause}
            ORDER BY CreatedAt DESC
            OFFSET @Offset ROWS
            FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
            FROM Security.AuditLogs
            {whereClause};";

        using var connection = CreateConnection();
        using var multi = await connection.QueryMultipleAsync(sql, parameters);

        var logs = (await multi.ReadAsync<AuditLog>()).ToList();
        var totalCount = await multi.ReadSingleAsync<int>();

        return (logs, totalCount);
    }
}
