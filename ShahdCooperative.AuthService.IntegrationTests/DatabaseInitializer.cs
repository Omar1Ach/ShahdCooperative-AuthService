using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ShahdCooperative.AuthService.IntegrationTests;

public static class DatabaseInitializer
{
    public static void Initialize(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        // Create Security schema and tables
        CreateSchema(connection);
        CreateTables(connection);
    }

    private static void CreateSchema(IDbConnection connection)
    {
        connection.Execute("IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Security') EXEC('CREATE SCHEMA Security')");
    }

    private static void CreateTables(IDbConnection connection)
    {
        // Users table
        connection.Execute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Security].[Users]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [Security].[Users] (
                    [Id] UNIQUEIDENTIFIER PRIMARY KEY,
                    [Email] NVARCHAR(255) NOT NULL UNIQUE,
                    [PasswordHash] NVARCHAR(MAX),
                    [PasswordSalt] NVARCHAR(MAX),
                    [Role] NVARCHAR(50) NOT NULL DEFAULT 'Customer',
                    [IsActive] BIT NOT NULL DEFAULT 1,
                    [IsEmailVerified] BIT NOT NULL DEFAULT 0,
                    [EmailVerificationToken] NVARCHAR(255),
                    [EmailVerificationExpiry] DATETIME2,
                    [PasswordResetToken] NVARCHAR(255),
                    [PasswordResetExpiry] DATETIME2,
                    [FailedLoginAttempts] INT NOT NULL DEFAULT 0,
                    [LockoutEnd] DATETIME2,
                    [LastLoginAt] DATETIME2,
                    [CreatedAt] DATETIME2 NOT NULL,
                    [UpdatedAt] DATETIME2 NOT NULL,
                    [IsDeleted] BIT NOT NULL DEFAULT 0,
                    [TwoFactorEnabled] BIT NOT NULL DEFAULT 0,
                    [TwoFactorSecret] NVARCHAR(MAX),
                    [BackupCodes] NVARCHAR(MAX),
                    [HasPassword] BIT NOT NULL DEFAULT 1
                )
            END
        ");

        // RefreshTokens table
        connection.Execute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Security].[RefreshTokens]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [Security].[RefreshTokens] (
                    [Id] UNIQUEIDENTIFIER PRIMARY KEY,
                    [UserId] UNIQUEIDENTIFIER NOT NULL,
                    [Token] NVARCHAR(255) NOT NULL UNIQUE,
                    [ExpiresAt] DATETIME2 NOT NULL,
                    [CreatedAt] DATETIME2 NOT NULL,
                    [RevokedAt] DATETIME2,
                    [ReplacedByToken] NVARCHAR(255),
                    [IsRevoked] BIT NOT NULL DEFAULT 0,
                    [DeviceInfo] NVARCHAR(500),
                    [IpAddress] NVARCHAR(50),
                    FOREIGN KEY ([UserId]) REFERENCES [Security].[Users]([Id])
                )
            END
        ");

        // AuditLogs table
        connection.Execute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Security].[AuditLogs]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [Security].[AuditLogs] (
                    [Id] UNIQUEIDENTIFIER PRIMARY KEY,
                    [UserId] UNIQUEIDENTIFIER,
                    [Action] NVARCHAR(100) NOT NULL,
                    [Details] NVARCHAR(MAX),
                    [IpAddress] NVARCHAR(50),
                    [UserAgent] NVARCHAR(500),
                    [Timestamp] DATETIME2 NOT NULL,
                    FOREIGN KEY ([UserId]) REFERENCES [Security].[Users]([Id])
                )
            END
        ");

        // PasswordResetTokens table
        connection.Execute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Security].[PasswordResetTokens]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [Security].[PasswordResetTokens] (
                    [Id] UNIQUEIDENTIFIER PRIMARY KEY,
                    [UserId] UNIQUEIDENTIFIER NOT NULL,
                    [Token] NVARCHAR(255) NOT NULL UNIQUE,
                    [ExpiresAt] DATETIME2 NOT NULL,
                    [CreatedAt] DATETIME2 NOT NULL,
                    [IsUsed] BIT NOT NULL DEFAULT 0,
                    FOREIGN KEY ([UserId]) REFERENCES [Security].[Users]([Id])
                )
            END
        ");

        // ExternalLogins table
        connection.Execute(@"
            IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[Security].[ExternalLogins]') AND type in (N'U'))
            BEGIN
                CREATE TABLE [Security].[ExternalLogins] (
                    [Id] UNIQUEIDENTIFIER PRIMARY KEY,
                    [UserId] UNIQUEIDENTIFIER NOT NULL,
                    [Provider] NVARCHAR(50) NOT NULL,
                    [ProviderKey] NVARCHAR(255) NOT NULL,
                    [Email] NVARCHAR(255) NOT NULL,
                    [ProviderDisplayName] NVARCHAR(255),
                    [LastLoginAt] DATETIME2,
                    [CreatedAt] DATETIME2 NOT NULL,
                    FOREIGN KEY ([UserId]) REFERENCES [Security].[Users]([Id])
                )
            END
        ");
    }
}
