namespace ShahdCooperative.AuthService.Domain.Interfaces;

public interface IEmailService
{
    Task SendEmailVerificationAsync(string email, string token);
    Task SendPasswordResetAsync(string email, string token);
    Task SendPasswordChangedNotificationAsync(string email);
}
