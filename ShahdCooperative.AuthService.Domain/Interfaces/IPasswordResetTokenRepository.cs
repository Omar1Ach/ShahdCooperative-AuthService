using ShahdCooperative.AuthService.Domain.Entities;

namespace ShahdCooperative.AuthService.Domain.Interfaces;

public interface IPasswordResetTokenRepository
{
    Task<Guid> CreateAsync(PasswordResetToken token);
    Task<PasswordResetToken?> GetByTokenAsync(string token);
    Task MarkAsUsedAsync(Guid tokenId);
    Task DeleteExpiredTokensAsync();
}
