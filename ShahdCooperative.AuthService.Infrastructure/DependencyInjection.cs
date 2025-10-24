using Microsoft.Extensions.DependencyInjection;
using ShahdCooperative.AuthService.Domain.Interfaces;
using ShahdCooperative.AuthService.Infrastructure.Repositories;
using ShahdCooperative.AuthService.Infrastructure.Services;

namespace ShahdCooperative.AuthService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();

        // Register services
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IMessagePublisher, RabbitMQPublisher>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<ICaptchaService, GoogleRecaptchaService>();
        services.AddScoped<ITwoFactorService, TwoFactorService>();

        // Register HttpClient for CAPTCHA verification
        services.AddHttpClient();

        return services;
    }
}
