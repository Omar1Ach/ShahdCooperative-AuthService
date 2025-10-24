using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ShahdCooperative.AuthService.Application.DTOs;
using ShahdCooperative.AuthService.Domain.Entities;
using ShahdCooperative.AuthService.Domain.Exceptions;
using ShahdCooperative.AuthService.Domain.Interfaces;
using RefreshTokenCommand = ShahdCooperative.AuthService.Application.Commands.RefreshToken.RefreshTokenCommand;
using RefreshTokenCommandHandler = ShahdCooperative.AuthService.Application.Commands.RefreshToken.RefreshTokenCommandHandler;

namespace ShahdCooperative.AuthService.Application.Tests.Commands.RefreshToken;

public class RefreshTokenCommandHandlerTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly RefreshTokenCommandHandler _handler;

    public RefreshTokenCommandHandlerTests()
    {
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _userRepositoryMock = new Mock<IUserRepository>();
        _tokenServiceMock = new Mock<ITokenService>();
        _mapperMock = new Mock<IMapper>();
        _configurationMock = new Mock<IConfiguration>();

        SetupConfiguration();

        _handler = new RefreshTokenCommandHandler(
            _userRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _tokenServiceMock.Object,
            _mapperMock.Object,
            _configurationMock.Object
        );
    }

    private void SetupConfiguration()
    {
        var jwtSectionMock = new Mock<IConfigurationSection>();
        jwtSectionMock.Setup(x => x["RefreshTokenExpiryDays"]).Returns("7");
        jwtSectionMock.Setup(x => x["AccessTokenExpiryMinutes"]).Returns("60");

        _configurationMock.Setup(x => x.GetSection("JwtSettings")).Returns(jwtSectionMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenRefreshTokenNotFound()
    {
        // Arrange
        var command = new RefreshTokenCommand("invalid-token");

        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync((Domain.Entities.RefreshToken?)null);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TokenExpiredException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenRefreshTokenIsRevoked()
    {
        // Arrange
        var command = new RefreshTokenCommand("revoked-token");
        var refreshToken = new Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "revoked-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            RevokedAt = DateTime.UtcNow.AddMinutes(-10)
        };

        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(command.RefreshToken))
            .ReturnsAsync(refreshToken);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TokenExpiredException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenRefreshTokenIsExpired()
    {
        // Arrange
        var command = new RefreshTokenCommand("expired-token");
        var refreshToken = new Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "expired-token",
            ExpiresAt = DateTime.UtcNow.AddDays(-1)
        };

        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(command.RefreshToken))
            .ReturnsAsync(refreshToken);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<TokenExpiredException>();
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenUserNotFound()
    {
        // Arrange
        var command = new RefreshTokenCommand("valid-token");
        var refreshToken = new Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            Token = "valid-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(command.RefreshToken))
            .ReturnsAsync(refreshToken);

        _userRepositoryMock.Setup(x => x.GetByIdAsync(refreshToken.UserId))
            .ReturnsAsync((User?)null);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<UserNotFoundException>();
    }

    [Fact]
    public async Task Handle_ShouldReturnNewTokens_WhenRefreshTokenIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new RefreshTokenCommand("valid-token");

        var refreshToken = new Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = "valid-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            Role = "Customer",
            IsActive = true
        };

        var newAccessToken = "new-access-token";
        var newRefreshToken = "new-refresh-token";

        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(command.RefreshToken))
            .ReturnsAsync(refreshToken);

        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user))
            .Returns(newAccessToken);

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns(newRefreshToken);

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAsync(command.RefreshToken, It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.RefreshToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(user))
            .Returns(new UserDto
            {
                Id = userId,
                Email = "test@example.com",
                Role = "Customer"
            });

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be(newAccessToken);
        result.RefreshToken.Should().Be(newRefreshToken);
        result.User.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task Handle_ShouldRevokeOldToken_BeforeCreatingNewOne()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new RefreshTokenCommand("valid-token");

        var refreshToken = new Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = "valid-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            Role = "Customer",
            IsActive = true
        };

        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(command.RefreshToken))
            .ReturnsAsync(refreshToken);

        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user))
            .Returns("access-token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh-token");

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAsync(command.RefreshToken, It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.RefreshToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(user))
            .Returns(new UserDto());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _refreshTokenRepositoryMock.Verify(x => x.RevokeAsync(command.RefreshToken, It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCreateNewRefreshToken_WithCorrectExpiry()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new RefreshTokenCommand("valid-token");

        var refreshToken = new Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = "valid-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            Role = "Customer",
            IsActive = true
        };

        var newRefreshTokenValue = "new-refresh-token";

        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(command.RefreshToken))
            .ReturnsAsync(refreshToken);

        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user))
            .Returns("access-token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns(newRefreshTokenValue);

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAsync(command.RefreshToken, It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.RefreshToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(user))
            .Returns(new UserDto());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _refreshTokenRepositoryMock.Verify(x => x.CreateAsync(It.Is<Domain.Entities.RefreshToken>(rt =>
            rt.UserId == userId &&
            rt.Token == newRefreshTokenValue &&
            rt.ExpiresAt > DateTime.UtcNow &&
            rt.ExpiresAt <= DateTime.UtcNow.AddDays(7).AddSeconds(5) &&
            rt.RevokedAt == null
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldGenerateNewAccessToken_ForValidUser()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new RefreshTokenCommand("valid-token");

        var refreshToken = new Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = "valid-token",
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            Role = "Customer",
            IsActive = true
        };

        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(command.RefreshToken))
            .ReturnsAsync(refreshToken);

        _userRepositoryMock.Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(user);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(user))
            .Returns("new-access-token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("new-refresh-token");

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAsync(command.RefreshToken, It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.RefreshToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(user))
            .Returns(new UserDto());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _tokenServiceMock.Verify(x => x.GenerateAccessToken(It.Is<User>(u =>
            u.Id == userId &&
            u.Email == "test@example.com"
        )), Times.Once);
    }
}
