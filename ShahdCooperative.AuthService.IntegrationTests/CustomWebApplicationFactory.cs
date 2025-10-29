using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private SqliteConnection? _connection;
    private readonly string _connectionString = "DataSource=:memory:";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Initialize SQLite database connection and keep it open for the lifetime of the factory
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
        DatabaseInitializer.Initialize(_connectionString);

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _connection?.Close();
            _connection?.Dispose();
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
