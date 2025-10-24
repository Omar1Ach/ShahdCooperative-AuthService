using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ShahdCooperative.AuthService.Domain.Entities;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Infrastructure.Repositories;

public class ExternalLoginRepository : IExternalLoginRepository
{
    private readonly string _connectionString;

    public ExternalLoginRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException(nameof(configuration), "Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<ExternalLogin?> GetByProviderAndKeyAsync(string provider, string providerKey)
    {
        const string sql = @"
            SELECT Id, UserId, Provider, ProviderKey, ProviderDisplayName, Email, CreatedAt, LastLoginAt
            FROM Security.ExternalLogins
            WHERE Provider = @Provider AND ProviderKey = @ProviderKey";

        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<ExternalLogin>(sql, new { Provider = provider, ProviderKey = providerKey });
    }

    public async Task<List<ExternalLogin>> GetByUserIdAsync(Guid userId)
    {
        const string sql = @"
            SELECT Id, UserId, Provider, ProviderKey, ProviderDisplayName, Email, CreatedAt, LastLoginAt
            FROM Security.ExternalLogins
            WHERE UserId = @UserId
            ORDER BY CreatedAt DESC";

        using var connection = CreateConnection();
        var result = await connection.QueryAsync<ExternalLogin>(sql, new { UserId = userId });
        return result.ToList();
    }

    public async Task<Guid> CreateAsync(ExternalLogin externalLogin)
    {
        const string sql = @"
            INSERT INTO Security.ExternalLogins
            (Id, UserId, Provider, ProviderKey, ProviderDisplayName, Email, CreatedAt, LastLoginAt)
            VALUES
            (@Id, @UserId, @Provider, @ProviderKey, @ProviderDisplayName, @Email, @CreatedAt, @LastLoginAt)";

        externalLogin.Id = Guid.NewGuid();
        externalLogin.CreatedAt = DateTime.UtcNow;

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, externalLogin);

        return externalLogin.Id;
    }

    public async Task UpdateLastLoginAsync(Guid id)
    {
        const string sql = @"
            UPDATE Security.ExternalLogins
            SET LastLoginAt = @LastLoginAt
            WHERE Id = @Id";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id, LastLoginAt = DateTime.UtcNow });
    }

    public async Task DeleteAsync(Guid id)
    {
        const string sql = @"
            DELETE FROM Security.ExternalLogins
            WHERE Id = @Id";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id });
    }

    public async Task<bool> ExistsAsync(string provider, string providerKey)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM Security.ExternalLogins
            WHERE Provider = @Provider AND ProviderKey = @ProviderKey";

        using var connection = CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(sql, new { Provider = provider, ProviderKey = providerKey });
        return count > 0;
    }
}
