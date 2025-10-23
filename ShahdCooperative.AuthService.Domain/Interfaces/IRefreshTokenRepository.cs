using ShahdCooperative.AuthService.Domain.Entities;

namespace ShahdCooperative.AuthService.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token);
    Task<RefreshToken?> GetActiveByUserIdAsync(Guid userId);
    Task<Guid> CreateAsync(RefreshToken refreshToken);
    Task RevokeAsync(string token, string? replacedByToken = null);
    Task RevokeAllByUserIdAsync(Guid userId);
    Task DeleteExpiredTokensAsync();
}
