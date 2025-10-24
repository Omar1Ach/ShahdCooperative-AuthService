using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ShahdCooperative.AuthService.Domain.Entities;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Infrastructure.Repositories;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly string _connectionString;

    public PasswordResetTokenRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string not found.");
    }

    public async Task<Guid> CreateAsync(PasswordResetToken token)
    {
        const string sql = @"
            INSERT INTO PasswordResetTokens (Id, UserId, Token, CreatedAt, ExpiresAt, UsedAt)
            VALUES (@Id, @UserId, @Token, @CreatedAt, @ExpiresAt, @UsedAt);
            SELECT @Id;";

        await using var connection = new SqlConnection(_connectionString);
        return await connection.ExecuteScalarAsync<Guid>(sql, token);
    }

    public async Task<PasswordResetToken?> GetByTokenAsync(string token)
    {
        const string sql = @"
            SELECT * FROM PasswordResetTokens
            WHERE Token = @Token";

        await using var connection = new SqlConnection(_connectionString);
        return await connection.QueryFirstOrDefaultAsync<PasswordResetToken>(sql, new { Token = token });
    }

    public async Task MarkAsUsedAsync(Guid tokenId)
    {
        const string sql = @"
            UPDATE PasswordResetTokens
            SET UsedAt = @UsedAt
            WHERE Id = @TokenId";

        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, new { TokenId = tokenId, UsedAt = DateTime.UtcNow });
    }

    public async Task DeleteExpiredTokensAsync()
    {
        const string sql = @"
            DELETE FROM PasswordResetTokens
            WHERE ExpiresAt < @Now";

        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(sql, new { Now = DateTime.UtcNow });
    }
}
