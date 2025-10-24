namespace ShahdCooperative.AuthService.Domain.Interfaces;

public interface ICaptchaService
{
    Task<bool> VerifyTokenAsync(string token, string? ipAddress = null);
}
