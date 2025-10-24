using ShahdCooperative.AuthService.Domain.Entities;

namespace ShahdCooperative.AuthService.Domain.Interfaces;

public interface IExternalLoginRepository
{
    Task<ExternalLogin?> GetByProviderAndKeyAsync(string provider, string providerKey);
    Task<List<ExternalLogin>> GetByUserIdAsync(Guid userId);
    Task<Guid> CreateAsync(ExternalLogin externalLogin);
    Task UpdateLastLoginAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<bool> ExistsAsync(string provider, string providerKey);
}
