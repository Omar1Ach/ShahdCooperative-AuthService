using ShahdCooperative.AuthService.Domain.Entities;

namespace ShahdCooperative.AuthService.Domain.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    Guid? ValidateAccessToken(string token);
}
