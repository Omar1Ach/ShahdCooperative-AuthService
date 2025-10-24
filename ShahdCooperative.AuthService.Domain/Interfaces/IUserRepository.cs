using ShahdCooperative.AuthService.Domain.Entities;

namespace ShahdCooperative.AuthService.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByEmailWithProfileAsync(string email);
    Task<Guid> CreateAsync(User user);
    Task UpdateAsync(User user);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(string email);
    Task IncrementFailedLoginAttemptsAsync(Guid userId);
    Task ResetFailedLoginAttemptsAsync(Guid userId);
    Task SetLockoutEndAsync(Guid userId, DateTime? lockoutEnd);
    Task UpdateLastLoginAsync(Guid userId);
    Task UpdatePasswordAsync(Guid userId, string passwordHash, string passwordSalt);
    Task VerifyEmailAsync(Guid userId);
    Task<User?> GetByEmailVerificationTokenAsync(string token);
}
