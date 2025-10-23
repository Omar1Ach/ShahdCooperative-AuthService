using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ShahdCooperative.AuthService.Domain.Entities;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Infrastructure.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly string _connectionString;

    public RefreshTokenRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException(nameof(configuration), "Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<RefreshToken?> GetByTokenAsync(string token)
    {
        const string sql = @"
            SELECT Id, UserId, Token, ExpiresAt, CreatedAt, RevokedAt, ReplacedByToken
            FROM Security.RefreshTokens
            WHERE Token = @Token";

        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<RefreshToken>(sql, new { Token = token });
    }

    public async Task<RefreshToken?> GetActiveByUserIdAsync(Guid userId)
    {
        const string sql = @"
            SELECT TOP 1 Id, UserId, Token, ExpiresAt, CreatedAt, RevokedAt, ReplacedByToken
            FROM Security.RefreshTokens
            WHERE UserId = @UserId
              AND RevokedAt IS NULL
              AND ExpiresAt > GETUTCDATE()
            ORDER BY CreatedAt DESC";

        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<RefreshToken>(sql, new { UserId = userId });
    }

    public async Task<Guid> CreateAsync(RefreshToken refreshToken)
    {
        const string sql = @"
            INSERT INTO Security.RefreshTokens
            (Id, UserId, Token, ExpiresAt, CreatedAt, RevokedAt, ReplacedByToken)
            VALUES
            (@Id, @UserId, @Token, @ExpiresAt, @CreatedAt, @RevokedAt, @ReplacedByToken)";

        refreshToken.Id = Guid.NewGuid();
        refreshToken.CreatedAt = DateTime.UtcNow;

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, refreshToken);

        return refreshToken.Id;
    }

    public async Task RevokeAsync(string token, string? replacedByToken = null)
    {
        const string sql = @"
            UPDATE Security.RefreshTokens
            SET RevokedAt = @RevokedAt, ReplacedByToken = @ReplacedByToken
            WHERE Token = @Token";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            Token = token,
            RevokedAt = DateTime.UtcNow,
            ReplacedByToken = replacedByToken
        });
    }

    public async Task RevokeAllByUserIdAsync(Guid userId)
    {
        const string sql = @"
            UPDATE Security.RefreshTokens
            SET RevokedAt = @RevokedAt
            WHERE UserId = @UserId AND RevokedAt IS NULL";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { UserId = userId, RevokedAt = DateTime.UtcNow });
    }

    public async Task DeleteExpiredTokensAsync()
    {
        const string sql = @"
            DELETE FROM Security.RefreshTokens
            WHERE ExpiresAt < DATEADD(day, -30, GETUTCDATE())";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql);
    }
}
