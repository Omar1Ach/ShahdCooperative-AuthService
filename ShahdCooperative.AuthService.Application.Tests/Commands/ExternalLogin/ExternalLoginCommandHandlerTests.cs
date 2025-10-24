using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ShahdCooperative.AuthService.Application.Commands.ExternalLogin;
using ShahdCooperative.AuthService.Application.DTOs;
using ShahdCooperative.AuthService.Domain.Entities;
using ShahdCooperative.AuthService.Domain.Interfaces;
using RefreshTokenEntity = ShahdCooperative.AuthService.Domain.Entities.RefreshToken;

namespace ShahdCooperative.AuthService.Application.Tests.Commands.ExternalLogin;

public class ExternalLoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IExternalLoginRepository> _externalLoginRepositoryMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IAuditLogRepository> _auditLogRepositoryMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IMessagePublisher> _messagePublisherMock;
    private readonly ExternalLoginCommandHandler _handler;

    public ExternalLoginCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _externalLoginRepositoryMock = new Mock<IExternalLoginRepository>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _auditLogRepositoryMock = new Mock<IAuditLogRepository>();
        _tokenServiceMock = new Mock<ITokenService>();
        _mapperMock = new Mock<IMapper>();
        _configurationMock = new Mock<IConfiguration>();
        _messagePublisherMock = new Mock<IMessagePublisher>();

        SetupConfiguration();

        _handler = new ExternalLoginCommandHandler(
            _userRepositoryMock.Object,
            _externalLoginRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _auditLogRepositoryMock.Object,
            _tokenServiceMock.Object,
            _mapperMock.Object,
            _configurationMock.Object,
            _messagePublisherMock.Object
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
    public async Task Handle_ShouldLoginExistingUser_WhenExternalLoginAlreadyExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var externalLoginId = Guid.NewGuid();
        var command = new ExternalLoginCommand(
            Provider: "Google",
            ProviderKey: "google-key-123",
            Email: "test@example.com",
            ProviderDisplayName: "Test User",
            IpAddress: "127.0.0.1",
            UserAgent: "Mozilla/5.0"
        );

        var existingExternalLogin = new Domain.Entities.ExternalLogin
        {
            Id = externalLoginId,
            UserId = userId,
            Provider = "Google",
            ProviderKey = "google-key-123",
            Email = "test@example.com"
        };

        var existingUser = new User
        {
            Id = userId,
            Email = "test@example.com",
            IsActive = true,
            Role = "Customer"
        };

        _externalLoginRepositoryMock
            .Setup(x => x.GetByProviderAndKeyAsync(command.Provider, command.ProviderKey))
            .ReturnsAsync(existingExternalLogin);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(existingUser);

        _externalLoginRepositoryMock
            .Setup(x => x.UpdateLastLoginAsync(externalLoginId))
            .Returns(Task.CompletedTask);

        _userRepositoryMock
            .Setup(x => x.UpdateLastLoginAsync(userId))
            .Returns(Task.CompletedTask);

        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh-token");

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAllByUserIdAsync(userId))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<RefreshTokenEntity>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto { Id = userId, Email = "test@example.com", Role = "Customer" });

        _messagePublisherMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.User.Email.Should().Be("test@example.com");
        result.Requires2FA.Should().BeFalse();

        _externalLoginRepositoryMock.Verify(x => x.UpdateLastLoginAsync(externalLoginId), Times.Once);
        _userRepositoryMock.Verify(x => x.UpdateLastLoginAsync(userId), Times.Once);
        _auditLogRepositoryMock.Verify(x => x.CreateAsync(It.Is<AuditLog>(log =>
            log.Action == "External Login" &&
            log.Result == "Success"
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenExternalLoginExistsButUserNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ExternalLoginCommand(
            Provider: "Google",
            ProviderKey: "google-key-123",
            Email: "test@example.com",
            ProviderDisplayName: "Test User",
            IpAddress: "127.0.0.1",
            UserAgent: "Mozilla/5.0"
        );

        var existingExternalLogin = new Domain.Entities.ExternalLogin
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = "Google",
            ProviderKey = "google-key-123"
        };

        _externalLoginRepositoryMock
            .Setup(x => x.GetByProviderAndKeyAsync(command.Provider, command.ProviderKey))
            .ReturnsAsync(existingExternalLogin);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync((User?)null);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("User associated with external login not found");
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenExistingUserIsInactive()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ExternalLoginCommand(
            Provider: "Google",
            ProviderKey: "google-key-123",
            Email: "test@example.com",
            ProviderDisplayName: "Test User",
            IpAddress: "127.0.0.1",
            UserAgent: "Mozilla/5.0"
        );

        var existingExternalLogin = new Domain.Entities.ExternalLogin
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = "Google",
            ProviderKey = "google-key-123"
        };

        var inactiveUser = new User
        {
            Id = userId,
            Email = "test@example.com",
            IsActive = false
        };

        _externalLoginRepositoryMock
            .Setup(x => x.GetByProviderAndKeyAsync(command.Provider, command.ProviderKey))
            .ReturnsAsync(existingExternalLogin);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(inactiveUser);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Account is inactive");
    }

    [Fact]
    public async Task Handle_ShouldLinkExternalLogin_WhenUserWithEmailExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ExternalLoginCommand(
            Provider: "Google",
            ProviderKey: "google-key-123",
            Email: "test@example.com",
            ProviderDisplayName: "Test User",
            IpAddress: "127.0.0.1",
            UserAgent: "Mozilla/5.0"
        );

        var existingUser = new User
        {
            Id = userId,
            Email = "test@example.com",
            IsActive = true,
            Role = "Customer"
        };

        _externalLoginRepositoryMock
            .Setup(x => x.GetByProviderAndKeyAsync(command.Provider, command.ProviderKey))
            .ReturnsAsync((Domain.Entities.ExternalLogin?)null);

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(existingUser);

        _externalLoginRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.ExternalLogin>()))
            .ReturnsAsync(Guid.NewGuid());

        _userRepositoryMock
            .Setup(x => x.UpdateLastLoginAsync(userId))
            .Returns(Task.CompletedTask);

        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh-token");

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAllByUserIdAsync(userId))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<RefreshTokenEntity>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto { Id = userId, Email = "test@example.com", Role = "Customer" });

        _messagePublisherMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");

        _externalLoginRepositoryMock.Verify(x => x.CreateAsync(It.Is<Domain.Entities.ExternalLogin>(el =>
            el.UserId == userId &&
            el.Provider == "Google" &&
            el.ProviderKey == "google-key-123" &&
            el.Email == "test@example.com"
        )), Times.Once);

        _auditLogRepositoryMock.Verify(x => x.CreateAsync(It.Is<AuditLog>(log =>
            log.Action == "External Login Linked" &&
            log.Result == "Success"
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCreateNewUser_WhenEmailDoesNotExist()
    {
        // Arrange
        var command = new ExternalLoginCommand(
            Provider: "Facebook",
            ProviderKey: "facebook-key-456",
            Email: "newuser@example.com",
            ProviderDisplayName: "New User",
            IpAddress: "127.0.0.1",
            UserAgent: "Mozilla/5.0"
        );

        var newUserId = Guid.NewGuid();

        _externalLoginRepositoryMock
            .Setup(x => x.GetByProviderAndKeyAsync(command.Provider, command.ProviderKey))
            .ReturnsAsync((Domain.Entities.ExternalLogin?)null);

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync("newuser@example.com"))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(newUserId);

        _externalLoginRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.ExternalLogin>()))
            .ReturnsAsync(Guid.NewGuid());

        _userRepositoryMock
            .Setup(x => x.UpdateLastLoginAsync(newUserId))
            .Returns(Task.CompletedTask);

        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh-token");

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAllByUserIdAsync(newUserId))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<RefreshTokenEntity>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto { Id = newUserId, Email = "newuser@example.com", Role = "Customer" });

        _messagePublisherMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        result.User.Email.Should().Be("newuser@example.com");

        _userRepositoryMock.Verify(x => x.CreateAsync(It.Is<User>(u =>
            u.Email == "newuser@example.com" &&
            u.PasswordHash == string.Empty &&
            u.PasswordSalt == string.Empty &&
            u.HasPassword == false &&
            u.IsEmailVerified == true &&
            u.Role == "Customer" &&
            u.IsActive == true
        )), Times.Once);

        _externalLoginRepositoryMock.Verify(x => x.CreateAsync(It.Is<Domain.Entities.ExternalLogin>(el =>
            el.UserId == newUserId &&
            el.Provider == "Facebook" &&
            el.ProviderKey == "facebook-key-456" &&
            el.Email == "newuser@example.com"
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPublishUserRegisteredEvent_WhenCreatingNewUser()
    {
        // Arrange
        var command = new ExternalLoginCommand(
            Provider: "Google",
            ProviderKey: "google-key-789",
            Email: "newuser@example.com",
            ProviderDisplayName: "New User",
            IpAddress: "127.0.0.1",
            UserAgent: "Mozilla/5.0"
        );

        var newUserId = Guid.NewGuid();

        _externalLoginRepositoryMock
            .Setup(x => x.GetByProviderAndKeyAsync(command.Provider, command.ProviderKey))
            .ReturnsAsync((Domain.Entities.ExternalLogin?)null);

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync("newuser@example.com"))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(newUserId);

        _externalLoginRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.ExternalLogin>()))
            .ReturnsAsync(Guid.NewGuid());

        _userRepositoryMock
            .Setup(x => x.UpdateLastLoginAsync(newUserId))
            .Returns(Task.CompletedTask);

        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh-token");

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAllByUserIdAsync(newUserId))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<RefreshTokenEntity>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto());

        _messagePublisherMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _messagePublisherMock.Verify(x => x.PublishAsync(
            It.Is<object>(e => e.GetType().Name == "UserRegisteredEvent"),
            "user.registered"
        ), Times.Once);

        _auditLogRepositoryMock.Verify(x => x.CreateAsync(It.Is<AuditLog>(log =>
            log.Action == "External Registration" &&
            log.Result == "Success"
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNormalizeEmail_ToLowerCase()
    {
        // Arrange
        var command = new ExternalLoginCommand(
            Provider: "Google",
            ProviderKey: "google-key-123",
            Email: "Test@Example.COM",
            ProviderDisplayName: "Test User",
            IpAddress: "127.0.0.1",
            UserAgent: "Mozilla/5.0"
        );

        _externalLoginRepositoryMock
            .Setup(x => x.GetByProviderAndKeyAsync(command.Provider, command.ProviderKey))
            .ReturnsAsync((Domain.Entities.ExternalLogin?)null);

        _userRepositoryMock
            .Setup(x => x.GetByEmailAsync("test@example.com"))
            .ReturnsAsync((User?)null);

        _userRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(Guid.NewGuid());

        _externalLoginRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.ExternalLogin>()))
            .ReturnsAsync(Guid.NewGuid());

        _userRepositoryMock
            .Setup(x => x.UpdateLastLoginAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh-token");

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAllByUserIdAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<RefreshTokenEntity>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto());

        _messagePublisherMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _userRepositoryMock.Verify(x => x.GetByEmailAsync("test@example.com"), Times.Once);
        _userRepositoryMock.Verify(x => x.CreateAsync(It.Is<User>(u =>
            u.Email == "test@example.com"
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRevokeOldRefreshTokens_BeforeCreatingNewOnes()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ExternalLoginCommand(
            Provider: "Google",
            ProviderKey: "google-key-123",
            Email: "test@example.com",
            ProviderDisplayName: "Test User",
            IpAddress: "127.0.0.1",
            UserAgent: "Mozilla/5.0"
        );

        var existingExternalLogin = new Domain.Entities.ExternalLogin
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = "Google",
            ProviderKey = "google-key-123"
        };

        var existingUser = new User
        {
            Id = userId,
            Email = "test@example.com",
            IsActive = true,
            Role = "Customer"
        };

        _externalLoginRepositoryMock
            .Setup(x => x.GetByProviderAndKeyAsync(command.Provider, command.ProviderKey))
            .ReturnsAsync(existingExternalLogin);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(existingUser);

        _externalLoginRepositoryMock
            .Setup(x => x.UpdateLastLoginAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _userRepositoryMock
            .Setup(x => x.UpdateLastLoginAsync(userId))
            .Returns(Task.CompletedTask);

        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh-token");

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAllByUserIdAsync(userId))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<RefreshTokenEntity>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto());

        _messagePublisherMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _refreshTokenRepositoryMock.Verify(x => x.RevokeAllByUserIdAsync(userId), Times.Once);
        _refreshTokenRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<RefreshTokenEntity>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPublishUserLoggedInEvent_ForAllScenarios()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var command = new ExternalLoginCommand(
            Provider: "Google",
            ProviderKey: "google-key-123",
            Email: "test@example.com",
            ProviderDisplayName: "Test User",
            IpAddress: "192.168.1.1",
            UserAgent: "Mozilla/5.0"
        );

        var existingExternalLogin = new Domain.Entities.ExternalLogin
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Provider = "Google",
            ProviderKey = "google-key-123"
        };

        var existingUser = new User
        {
            Id = userId,
            Email = "test@example.com",
            IsActive = true
        };

        _externalLoginRepositoryMock
            .Setup(x => x.GetByProviderAndKeyAsync(command.Provider, command.ProviderKey))
            .ReturnsAsync(existingExternalLogin);

        _userRepositoryMock
            .Setup(x => x.GetByIdAsync(userId))
            .ReturnsAsync(existingUser);

        _externalLoginRepositoryMock
            .Setup(x => x.UpdateLastLoginAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _userRepositoryMock
            .Setup(x => x.UpdateLastLoginAsync(userId))
            .Returns(Task.CompletedTask);

        _auditLogRepositoryMock
            .Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh-token");

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAllByUserIdAsync(userId))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<RefreshTokenEntity>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto());

        _messagePublisherMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _messagePublisherMock.Verify(x => x.PublishAsync(
            It.Is<object>(e => e.GetType().Name == "UserLoggedInEvent"),
            "user.logged-in"
        ), Times.Once);
    }
}
