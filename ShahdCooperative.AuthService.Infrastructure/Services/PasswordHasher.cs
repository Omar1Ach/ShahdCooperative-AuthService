using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Infrastructure.Services;

public class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string HashPassword(string password, out string salt)
    {
        salt = BCrypt.Net.BCrypt.GenerateSalt(WorkFactor);
        return BCrypt.Net.BCrypt.HashPassword(password, salt);
    }

    public bool VerifyPassword(string password, string hash, string salt)
    {
        return BCrypt.Net.BCrypt.Verify(password, hash);
    }
}
