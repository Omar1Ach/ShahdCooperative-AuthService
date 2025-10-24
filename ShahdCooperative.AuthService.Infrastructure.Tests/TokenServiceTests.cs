using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ShahdCooperative.AuthService.Domain.Entities;
using ShahdCooperative.AuthService.Infrastructure.Services;

namespace ShahdCooperative.AuthService.Infrastructure.Tests;

public class TokenServiceTests
{
    private readonly TokenService _tokenService;
    private readonly IConfiguration _configuration;

    public TokenServiceTests()
    {
        var config = new Dictionary<string, string>
        {
            { "JwtSettings:SecretKey", "YourSuperSecretKeyForJWTThatIsAtLeast32CharactersLong!" },
            { "JwtSettings:Issuer", "TestIssuer" },
            { "JwtSettings:Audience", "TestAudience" },
            { "JwtSettings:AccessTokenExpiryMinutes", "60" }
        };

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config!)
            .Build();

        _tokenService = new TokenService(_configuration);
    }

    [Fact]
    public void GenerateAccessToken_ShouldReturnValidJwtToken()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Role = "Customer"
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        handler.CanReadToken(token).Should().BeTrue();
    }

    [Fact]
    public void GenerateAccessToken_ShouldContainCorrectClaims()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var email = "test@example.com";
        var role = "Customer";

        var user = new User
        {
            Id = userId,
            Email = email,
            Role = role
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == userId.ToString());
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == email);
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == role);
    }

    [Fact]
    public void GenerateAccessToken_ShouldHaveCorrectIssuerAndAudience()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Role = "Customer"
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Issuer.Should().Be("TestIssuer");
        jwtToken.Audiences.Should().Contain("TestAudience");
    }

    [Fact]
    public void GenerateAccessToken_ShouldHaveExpirationSet()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            Role = "Customer"
        };

        // Act
        var token = _tokenService.GenerateAccessToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.ValidTo.Should().BeAfter(DateTime.UtcNow);
        jwtToken.ValidTo.Should().BeCloseTo(DateTime.UtcNow.AddMinutes(60), TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnNonEmptyString()
    {
        // Act
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Assert
        refreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnDifferentTokensOnEachCall()
    {
        // Act
        var token1 = _tokenService.GenerateRefreshToken();
        var token2 = _tokenService.GenerateRefreshToken();

        // Assert
        token1.Should().NotBe(token2);
    }

    [Fact]
    public void GenerateRefreshToken_ShouldReturnBase64String()
    {
        // Act
        var refreshToken = _tokenService.GenerateRefreshToken();

        // Assert
        var act = () => Convert.FromBase64String(refreshToken);
        act.Should().NotThrow();
    }
}
