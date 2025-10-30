using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _databaseName = $"AuthService_IntegrationTests_{Guid.NewGuid():N}";

    public CustomWebApplicationFactory()
    {
        // Use SQL Server LocalDB for Windows or provide Docker connection string
        // For LocalDB: "Server=(localdb)\\mssqllocaldb;Database={dbName};Trusted_Connection=True;MultipleActiveResultSets=true"
        // For Docker: "Server=localhost,1433;Database={dbName};User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True"

        var useDocker = Environment.GetEnvironmentVariable("USE_DOCKER_SQL") == "true";

        if (useDocker)
        {
            _connectionString = $"Server=localhost,1433;Database={_databaseName};User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;";
        }
        else
        {
            _connectionString = $"Server=(localdb)\\mssqllocaldb;Database={_databaseName};Trusted_Connection=True;MultipleActiveResultSets=true";
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Initialize SQL Server database
        InitializeDatabase();

        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["JwtSettings:SecretKey"] = "TestSecretKeyForIntegrationTesting_MustBeAtLeast32Characters!",
                ["JwtSettings:Issuer"] = "TestIssuer",
                ["JwtSettings:Audience"] = "TestAudience",
                ["JwtSettings:AccessTokenExpirationMinutes"] = "15",
                ["JwtSettings:RefreshTokenExpirationDays"] = "7",
                ["OAuth:Google:ClientId"] = "test-google-client-id",
                ["OAuth:Google:ClientSecret"] = "test-google-client-secret",
                ["OAuth:Facebook:AppId"] = "test-facebook-app-id",
                ["OAuth:Facebook:AppSecret"] = "test-facebook-app-secret",
                ["AppSettings:FrontendUrl"] = "http://localhost:3000",
                ["RateLimiting:Auth:Limit"] = "1000",
                ["RateLimiting:Api:Limit"] = "1000",
                ["RateLimiting:Admin:Limit"] = "1000"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Replace external services with fakes for testing
            services.RemoveAll<IMessagePublisher>();
            services.AddSingleton<IMessagePublisher, FakeMessagePublisher>();

            services.RemoveAll<IEmailService>();
            services.AddSingleton<IEmailService, FakeEmailService>();

            services.RemoveAll<ICaptchaService>();
            services.AddSingleton<ICaptchaService, FakeCaptchaService>();
        });
    }

    private void InitializeDatabase()
    {
        try
        {
            // Create database if it doesn't exist
            var masterConnection = _connectionString.Replace(_databaseName, "master");
            using (var connection = new SqlConnection(masterConnection))
            {
                connection.Open();
                var checkDbSql = $"SELECT database_id FROM sys.databases WHERE name = '{_databaseName}'";
                var dbExists = connection.ExecuteScalar<int?>(checkDbSql);

                if (dbExists == null)
                {
                    connection.Execute($"CREATE DATABASE [{_databaseName}]");
                }
            }

            // Initialize schema
            DatabaseInitializer.Initialize(_connectionString);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Database initialization failed: {ex.Message}");
            Console.WriteLine("Make sure SQL Server LocalDB is installed or Docker SQL Server is running.");
            throw;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                // Drop test database
                var masterConnection = _connectionString.Replace(_databaseName, "master");
                using var connection = new SqlConnection(masterConnection);
                connection.Open();
                connection.Execute($"DROP DATABASE IF EXISTS [{_databaseName}]");
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        base.Dispose(disposing);
    }
}

// Fake implementations for testing
public class FakeMessagePublisher : IMessagePublisher
{
    public Task PublishAsync<T>(T message, string routingKey) where T : class
    {
        return Task.CompletedTask;
    }
}

public class FakeEmailService : IEmailService
{
    public Task SendEmailVerificationAsync(string email, string verificationToken)
    {
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string resetToken)
    {
        return Task.CompletedTask;
    }

    public Task SendPasswordChangedNotificationAsync(string email)
    {
        return Task.CompletedTask;
    }

    public Task SendAccountLockedNotificationAsync(string email, DateTime lockoutEnd)
    {
        return Task.CompletedTask;
    }

    public Task SendNewDeviceLoginNotificationAsync(string email, string deviceInfo, string ipAddress)
    {
        return Task.CompletedTask;
    }

    public Task SendTwoFactorCodeAsync(string email, string code)
    {
        return Task.CompletedTask;
    }
}

public class FakeCaptchaService : ICaptchaService
{
    public Task<bool> VerifyTokenAsync(string token, string? ipAddress = null)
    {
        // Always return true for tests
        return Task.FromResult(true);
    }
}
