using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace ShahdCooperative.AuthService.IntegrationTests;

public static class DatabaseInitializer
{
    public static void Initialize(string connectionString)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        // Create Security schema (SQLite doesn't have schemas, but we'll use tables with prefix)
        CreateTables(connection);
    }

    private static void CreateTables(IDbConnection connection)
    {
        // Users table
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS Users (
                Id TEXT PRIMARY KEY,
                Email TEXT NOT NULL UNIQUE,
                PasswordHash TEXT,
                PasswordSalt TEXT,
                Role TEXT NOT NULL DEFAULT 'Customer',
                IsActive INTEGER NOT NULL DEFAULT 1,
                IsEmailVerified INTEGER NOT NULL DEFAULT 0,
                EmailVerificationToken TEXT,
                EmailVerificationExpiry TEXT,
                PasswordResetToken TEXT,
                PasswordResetExpiry TEXT,
                FailedLoginAttempts INTEGER NOT NULL DEFAULT 0,
                LockoutEnd TEXT,
                LastLoginAt TEXT,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                IsDeleted INTEGER NOT NULL DEFAULT 0,
                TwoFactorEnabled INTEGER NOT NULL DEFAULT 0,
                TwoFactorSecret TEXT,
                BackupCodes TEXT,
                HasPassword INTEGER NOT NULL DEFAULT 1
            )
        ");

        // RefreshTokens table
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS RefreshTokens (
                Id TEXT PRIMARY KEY,
                UserId TEXT NOT NULL,
                Token TEXT NOT NULL UNIQUE,
                ExpiresAt TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                RevokedAt TEXT,
                IsRevoked INTEGER NOT NULL DEFAULT 0,
                DeviceInfo TEXT,
                IpAddress TEXT,
                FOREIGN KEY (UserId) REFERENCES Users(Id)
            )
        ");

        // AuditLogs table
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS AuditLogs (
                Id TEXT PRIMARY KEY,
                UserId TEXT,
                Action TEXT NOT NULL,
                Details TEXT,
                IpAddress TEXT,
                UserAgent TEXT,
                Timestamp TEXT NOT NULL,
                FOREIGN KEY (UserId) REFERENCES Users(Id)
            )
        ");

        // PasswordResetTokens table
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS PasswordResetTokens (
                Id TEXT PRIMARY KEY,
                UserId TEXT NOT NULL,
                Token TEXT NOT NULL UNIQUE,
                ExpiresAt TEXT NOT NULL,
                CreatedAt TEXT NOT NULL,
                IsUsed INTEGER NOT NULL DEFAULT 0,
                FOREIGN KEY (UserId) REFERENCES Users(Id)
            )
        ");

        // ExternalLogins table
        connection.Execute(@"
            CREATE TABLE IF NOT EXISTS ExternalLogins (
                Id TEXT PRIMARY KEY,
                UserId TEXT NOT NULL,
                Provider TEXT NOT NULL,
                ProviderKey TEXT NOT NULL,
                Email TEXT NOT NULL,
                ProviderDisplayName TEXT,
                LastLoginAt TEXT,
                CreatedAt TEXT NOT NULL,
                FOREIGN KEY (UserId) REFERENCES Users(Id)
            )
        ");
    }
}
