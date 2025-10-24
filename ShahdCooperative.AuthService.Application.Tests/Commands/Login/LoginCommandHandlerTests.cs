using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ShahdCooperative.AuthService.Application.DTOs;
using ShahdCooperative.AuthService.Domain.Entities;
using ShahdCooperative.AuthService.Domain.Exceptions;
using ShahdCooperative.AuthService.Domain.Interfaces;
using LoginCommand = ShahdCooperative.AuthService.Application.Commands.Login.LoginCommand;
using LoginCommandHandler = ShahdCooperative.AuthService.Application.Commands.Login.LoginCommandHandler;

namespace ShahdCooperative.AuthService.Application.Tests.Commands.Login;

public class LoginCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IAuditLogRepository> _auditLogRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IMessagePublisher> _messagePublisherMock;
    private readonly LoginCommandHandler _handler;

    public LoginCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _auditLogRepositoryMock = new Mock<IAuditLogRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _tokenServiceMock = new Mock<ITokenService>();
        _mapperMock = new Mock<IMapper>();
        _configurationMock = new Mock<IConfiguration>();
        _messagePublisherMock = new Mock<IMessagePublisher>();

        SetupConfiguration();

        _handler = new LoginCommandHandler(
            _userRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _auditLogRepositoryMock.Object,
            _passwordHasherMock.Object,
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
    public async Task Handle_ShouldThrowException_WhenUserNotFound()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "Password123!", null, null);

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidCredentialsException>();

        _auditLogRepositoryMock.Verify(x => x.CreateAsync(It.Is<AuditLog>(log =>
            log.Action == "Login" &&
            log.Result == "Failed" &&
            log.Details!.Contains("User not found")
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenAccountIsLocked()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "Password123!", null, null);
        var lockedUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            LockoutEnd = DateTime.UtcNow.AddMinutes(10),
            IsActive = true
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(lockedUser);

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AccountLockedException>();

        _auditLogRepositoryMock.Verify(x => x.CreateAsync(It.Is<AuditLog>(log =>
            log.Action == "Login" &&
            log.Result == "Failed" &&
            log.Details!.Contains("Account locked")
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenAccountIsInactive()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "Password123!", null, null);
        var inactiveUser = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            IsActive = false
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(inactiveUser);

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidCredentialsException>()
            .WithMessage("Account is inactive.");

        _auditLogRepositoryMock.Verify(x => x.CreateAsync(It.Is<AuditLog>(log =>
            log.Action == "Login" &&
            log.Result == "Failed" &&
            log.Details!.Contains("Account inactive")
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldThrowException_WhenPasswordIsInvalid()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "WrongPassword", null, null);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsActive = true,
            FailedLoginAttempts = 0
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);

        _passwordHasherMock.Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        _userRepositoryMock.Setup(x => x.IncrementFailedLoginAttemptsAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _userRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new User { Id = user.Id, FailedLoginAttempts = 1 });

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidCredentialsException>();

        _userRepositoryMock.Verify(x => x.IncrementFailedLoginAttemptsAsync(user.Id), Times.Once);

        _auditLogRepositoryMock.Verify(x => x.CreateAsync(It.Is<AuditLog>(log =>
            log.Action == "Login" &&
            log.Result == "Failed" &&
            log.Details!.Contains("Invalid password")
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLockAccount_AfterMaxFailedAttempts()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "WrongPassword", null, null);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsActive = true,
            FailedLoginAttempts = 4
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);

        _passwordHasherMock.Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(false);

        _userRepositoryMock.Setup(x => x.IncrementFailedLoginAttemptsAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _userRepositoryMock.Setup(x => x.GetByIdAsync(It.IsAny<Guid>()))
            .ReturnsAsync(new User { Id = user.Id, FailedLoginAttempts = 5 });

        _userRepositoryMock.Setup(x => x.SetLockoutEndAsync(It.IsAny<Guid>(), It.IsAny<DateTime>()))
            .Returns(Task.CompletedTask);

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<AccountLockedException>();

        _userRepositoryMock.Verify(x => x.SetLockoutEndAsync(
            user.Id,
            It.Is<DateTime>(dt => dt > DateTime.UtcNow && dt <= DateTime.UtcNow.AddMinutes(15).AddSeconds(5))
        ), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnTokens_WhenCredentialsAreValid()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "Password123!", "127.0.0.1", "TestAgent");
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsActive = true,
            Role = "Customer"
        };

        var accessToken = "access-token";
        var refreshToken = "refresh-token";

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);

        _passwordHasherMock.Setup(x => x.VerifyPassword(command.Password, user.PasswordHash, user.PasswordSalt))
            .Returns(true);

        _userRepositoryMock.Setup(x => x.ResetFailedLoginAttemptsAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _userRepositoryMock.Setup(x => x.UpdateLastLoginAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns(accessToken);

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns(refreshToken);

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAllByUserIdAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.RefreshToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto { Id = userId, Email = "test@example.com" });

        _messagePublisherMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be(accessToken);
        result.RefreshToken.Should().Be(refreshToken);
        result.User.Email.Should().Be("test@example.com");
    }

    [Fact]
    public async Task Handle_ShouldResetFailedLoginAttempts_OnSuccessfulLogin()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "Password123!", null, null);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsActive = true,
            FailedLoginAttempts = 3
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);

        _passwordHasherMock.Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        _userRepositoryMock.Setup(x => x.ResetFailedLoginAttemptsAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _userRepositoryMock.Setup(x => x.UpdateLastLoginAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh");

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAllByUserIdAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.RefreshToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _userRepositoryMock.Verify(x => x.ResetFailedLoginAttemptsAsync(user.Id), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldRevokeOldRefreshTokens_BeforeCreatingNew()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "Password123!", null, null);
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsActive = true
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);

        _passwordHasherMock.Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        _userRepositoryMock.Setup(x => x.ResetFailedLoginAttemptsAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _userRepositoryMock.Setup(x => x.UpdateLastLoginAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh");

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAllByUserIdAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.RefreshToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _refreshTokenRepositoryMock.Verify(x => x.RevokeAllByUserIdAsync(userId), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLogSuccessfulLogin_InAuditLog()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "Password123!", "192.168.1.1", "Mozilla");
        var userId = Guid.NewGuid();
        var user = new User
        {
            Id = userId,
            Email = "test@example.com",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsActive = true
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);

        _passwordHasherMock.Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        _userRepositoryMock.Setup(x => x.ResetFailedLoginAttemptsAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _userRepositoryMock.Setup(x => x.UpdateLastLoginAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh");

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAllByUserIdAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.RefreshToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto());

        _messagePublisherMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _auditLogRepositoryMock.Verify(x => x.CreateAsync(It.Is<AuditLog>(log =>
            log.UserId == userId &&
            log.Action == "Login" &&
            log.Result == "Success" &&
            log.IpAddress == "192.168.1.1" &&
            log.UserAgent == "Mozilla"
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPublishUserLoggedInEvent_OnSuccessfulLogin()
    {
        // Arrange
        var command = new LoginCommand("test@example.com", "Password123!", "127.0.0.1", null);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = "hash",
            PasswordSalt = "salt",
            IsActive = true
        };

        _userRepositoryMock.Setup(x => x.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);

        _passwordHasherMock.Setup(x => x.VerifyPassword(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(true);

        _userRepositoryMock.Setup(x => x.ResetFailedLoginAttemptsAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _userRepositoryMock.Setup(x => x.UpdateLastLoginAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh");

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAllByUserIdAsync(It.IsAny<Guid>()))
            .Returns(Task.CompletedTask);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.RefreshToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
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
