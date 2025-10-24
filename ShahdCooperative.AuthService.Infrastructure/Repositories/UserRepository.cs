using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ShahdCooperative.AuthService.Domain.Entities;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly string _connectionString;

    public UserRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new ArgumentNullException(nameof(configuration), "Connection string 'DefaultConnection' not found.");
    }

    private IDbConnection CreateConnection() => new SqlConnection(_connectionString);

    public async Task<User?> GetByIdAsync(Guid id)
    {
        const string sql = @"
            SELECT Id, Email, PasswordHash, PasswordSalt, Role, IsActive, IsEmailVerified,
                   EmailVerificationToken, EmailVerificationExpiry, PasswordResetToken,
                   PasswordResetExpiry, FailedLoginAttempts, LockoutEnd, LastLoginAt,
                   CreatedAt, UpdatedAt, IsDeleted, TwoFactorEnabled, TwoFactorSecret, BackupCodes
            FROM Security.Users
            WHERE Id = @Id AND IsDeleted = 0";

        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Id = id });
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        const string sql = @"
            SELECT Id, Email, PasswordHash, PasswordSalt, Role, IsActive, IsEmailVerified,
                   EmailVerificationToken, EmailVerificationExpiry, PasswordResetToken,
                   PasswordResetExpiry, FailedLoginAttempts, LockoutEnd, LastLoginAt,
                   CreatedAt, UpdatedAt, IsDeleted, TwoFactorEnabled, TwoFactorSecret, BackupCodes
            FROM Security.Users
            WHERE Email = @Email AND IsDeleted = 0";

        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Email = email });
    }

    public async Task<User?> GetByEmailWithProfileAsync(string email)
    {
        const string sql = @"
            SELECT u.Id, u.Email, u.PasswordHash, u.PasswordSalt, u.Role, u.IsActive, u.IsEmailVerified,
                   u.EmailVerificationToken, u.EmailVerificationExpiry, u.PasswordResetToken,
                   u.PasswordResetExpiry, u.FailedLoginAttempts, u.LockoutEnd, u.LastLoginAt,
                   u.CreatedAt, u.UpdatedAt, u.IsDeleted, u.TwoFactorEnabled, u.TwoFactorSecret, u.BackupCodes,
                   p.Id, p.UserId, p.FirstName, p.LastName, p.PhoneNumber, p.Address,
                   p.City, p.Country, p.DateOfBirth, p.ProfilePictureUrl, p.CreatedAt, p.UpdatedAt
            FROM Security.Users u
            LEFT JOIN Security.UserProfiles p ON u.Id = p.UserId
            WHERE u.Email = @Email AND u.IsDeleted = 0";

        using var connection = CreateConnection();

        var users = await connection.QueryAsync<User, UserProfile, User>(
            sql,
            (user, profile) =>
            {
                user.Profile = profile;
                return user;
            },
            new { Email = email },
            splitOn: "Id"
        );

        return users.FirstOrDefault();
    }

    public async Task<Guid> CreateAsync(User user)
    {
        const string sql = @"
            INSERT INTO Security.Users
            (Id, Email, PasswordHash, PasswordSalt, Role, IsActive, IsEmailVerified,
             EmailVerificationToken, EmailVerificationExpiry, PasswordResetToken,
             PasswordResetExpiry, FailedLoginAttempts, LockoutEnd, LastLoginAt,
             CreatedAt, UpdatedAt, IsDeleted, TwoFactorEnabled, TwoFactorSecret, BackupCodes)
            VALUES
            (@Id, @Email, @PasswordHash, @PasswordSalt, @Role, @IsActive, @IsEmailVerified,
             @EmailVerificationToken, @EmailVerificationExpiry, @PasswordResetToken,
             @PasswordResetExpiry, @FailedLoginAttempts, @LockoutEnd, @LastLoginAt,
             @CreatedAt, @UpdatedAt, @IsDeleted, @TwoFactorEnabled, @TwoFactorSecret, @BackupCodes)";

        user.Id = Guid.NewGuid();
        user.CreatedAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, user);

        return user.Id;
    }

    public async Task UpdateAsync(User user)
    {
        const string sql = @"
            UPDATE Security.Users
            SET Email = @Email, PasswordHash = @PasswordHash, PasswordSalt = @PasswordSalt,
                Role = @Role, IsActive = @IsActive, IsEmailVerified = @IsEmailVerified,
                EmailVerificationToken = @EmailVerificationToken, EmailVerificationExpiry = @EmailVerificationExpiry,
                PasswordResetToken = @PasswordResetToken, PasswordResetExpiry = @PasswordResetExpiry,
                FailedLoginAttempts = @FailedLoginAttempts, LockoutEnd = @LockoutEnd,
                LastLoginAt = @LastLoginAt, UpdatedAt = @UpdatedAt, IsDeleted = @IsDeleted,
                TwoFactorEnabled = @TwoFactorEnabled, TwoFactorSecret = @TwoFactorSecret, BackupCodes = @BackupCodes
            WHERE Id = @Id";

        user.UpdatedAt = DateTime.UtcNow;

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, user);
    }

    public async Task DeleteAsync(Guid id)
    {
        const string sql = @"
            UPDATE Security.Users
            SET IsDeleted = 1, UpdatedAt = @UpdatedAt
            WHERE Id = @Id";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { Id = id, UpdatedAt = DateTime.UtcNow });
    }

    public async Task<bool> ExistsAsync(string email)
    {
        const string sql = @"
            SELECT COUNT(1)
            FROM Security.Users
            WHERE Email = @Email AND IsDeleted = 0";

        using var connection = CreateConnection();
        var count = await connection.ExecuteScalarAsync<int>(sql, new { Email = email });
        return count > 0;
    }

    public async Task IncrementFailedLoginAttemptsAsync(Guid userId)
    {
        const string sql = @"
            UPDATE Security.Users
            SET FailedLoginAttempts = FailedLoginAttempts + 1, UpdatedAt = @UpdatedAt
            WHERE Id = @UserId";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { UserId = userId, UpdatedAt = DateTime.UtcNow });
    }

    public async Task ResetFailedLoginAttemptsAsync(Guid userId)
    {
        const string sql = @"
            UPDATE Security.Users
            SET FailedLoginAttempts = 0, LockoutEnd = NULL, UpdatedAt = @UpdatedAt
            WHERE Id = @UserId";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { UserId = userId, UpdatedAt = DateTime.UtcNow });
    }

    public async Task SetLockoutEndAsync(Guid userId, DateTime? lockoutEnd)
    {
        const string sql = @"
            UPDATE Security.Users
            SET LockoutEnd = @LockoutEnd, UpdatedAt = @UpdatedAt
            WHERE Id = @UserId";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { UserId = userId, LockoutEnd = lockoutEnd, UpdatedAt = DateTime.UtcNow });
    }

    public async Task UpdateLastLoginAsync(Guid userId)
    {
        const string sql = @"
            UPDATE Security.Users
            SET LastLoginAt = @LastLoginAt, UpdatedAt = @UpdatedAt
            WHERE Id = @UserId";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { UserId = userId, LastLoginAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
    }

    public async Task UpdatePasswordAsync(Guid userId, string passwordHash, string passwordSalt)
    {
        const string sql = @"
            UPDATE Security.Users
            SET PasswordHash = @PasswordHash, PasswordSalt = @PasswordSalt, UpdatedAt = @UpdatedAt
            WHERE Id = @UserId";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new
        {
            UserId = userId,
            PasswordHash = passwordHash,
            PasswordSalt = passwordSalt,
            UpdatedAt = DateTime.UtcNow
        });
    }

    public async Task VerifyEmailAsync(Guid userId)
    {
        const string sql = @"
            UPDATE Security.Users
            SET IsEmailVerified = 1,
                EmailVerificationToken = NULL,
                EmailVerificationExpiry = NULL,
                UpdatedAt = @UpdatedAt
            WHERE Id = @UserId";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { UserId = userId, UpdatedAt = DateTime.UtcNow });
    }

    public async Task<User?> GetByEmailVerificationTokenAsync(string token)
    {
        const string sql = @"
            SELECT Id, Email, PasswordHash, PasswordSalt, Role, IsActive, IsEmailVerified,
                   EmailVerificationToken, EmailVerificationExpiry, PasswordResetToken,
                   PasswordResetExpiry, FailedLoginAttempts, LockoutEnd, LastLoginAt,
                   CreatedAt, UpdatedAt, IsDeleted, TwoFactorEnabled, TwoFactorSecret, BackupCodes
            FROM Security.Users
            WHERE EmailVerificationToken = @Token AND IsDeleted = 0";

        using var connection = CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<User>(sql, new { Token = token });
    }

    public async Task<(List<User> Users, int TotalCount)> GetAllAsync(int pageNumber, int pageSize, string? searchTerm, string? sortBy, bool sortDescending)
    {
        var offset = (pageNumber - 1) * pageSize;
        var orderBy = sortBy switch
        {
            "email" => "Email",
            "role" => "Role",
            "createdAt" => "CreatedAt",
            "lastLoginAt" => "LastLoginAt",
            _ => "CreatedAt"
        };
        var orderDirection = sortDescending ? "DESC" : "ASC";

        var whereClause = string.IsNullOrWhiteSpace(searchTerm)
            ? "WHERE IsDeleted = 0"
            : "WHERE IsDeleted = 0 AND Email LIKE @SearchTerm";

        var sql = $@"
            SELECT Id, Email, PasswordHash, PasswordSalt, Role, IsActive, IsEmailVerified,
                   EmailVerificationToken, EmailVerificationExpiry, PasswordResetToken,
                   PasswordResetExpiry, FailedLoginAttempts, LockoutEnd, LastLoginAt,
                   CreatedAt, UpdatedAt, IsDeleted
            FROM Security.Users
            {whereClause}
            ORDER BY {orderBy} {orderDirection}
            OFFSET @Offset ROWS
            FETCH NEXT @PageSize ROWS ONLY;

            SELECT COUNT(*)
            FROM Security.Users
            {whereClause};";

        using var connection = CreateConnection();
        using var multi = await connection.QueryMultipleAsync(sql, new
        {
            SearchTerm = searchTerm != null ? $"%{searchTerm}%" : null,
            Offset = offset,
            PageSize = pageSize
        });

        var users = (await multi.ReadAsync<User>()).ToList();
        var totalCount = await multi.ReadSingleAsync<int>();

        return (users, totalCount);
    }

    public async Task UpdateRoleAsync(Guid userId, string role)
    {
        const string sql = @"
            UPDATE Security.Users
            SET Role = @Role, UpdatedAt = @UpdatedAt
            WHERE Id = @UserId";

        using var connection = CreateConnection();
        await connection.ExecuteAsync(sql, new { UserId = userId, Role = role, UpdatedAt = DateTime.UtcNow });
    }
}
