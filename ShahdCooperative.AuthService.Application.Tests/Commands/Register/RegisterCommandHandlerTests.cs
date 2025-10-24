using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ShahdCooperative.AuthService.Application.DTOs;
using ShahdCooperative.AuthService.Domain.Entities;
using ShahdCooperative.AuthService.Domain.Interfaces;
using RegisterCommand = ShahdCooperative.AuthService.Application.Commands.Register.RegisterCommand;
using RegisterCommandHandler = ShahdCooperative.AuthService.Application.Commands.Register.RegisterCommandHandler;

namespace ShahdCooperative.AuthService.Application.Tests.Commands.Register;

public class RegisterCommandHandlerTests
{
    private readonly Mock<IUserRepository> _userRepositoryMock;
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IAuditLogRepository> _auditLogRepositoryMock;
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly Mock<ITokenService> _tokenServiceMock;
    private readonly Mock<IMapper> _mapperMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<IMessagePublisher> _messagePublisherMock;
    private readonly Mock<IEmailService> _emailServiceMock;
    private readonly RegisterCommandHandler _handler;

    public RegisterCommandHandlerTests()
    {
        _userRepositoryMock = new Mock<IUserRepository>();
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _auditLogRepositoryMock = new Mock<IAuditLogRepository>();
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _tokenServiceMock = new Mock<ITokenService>();
        _mapperMock = new Mock<IMapper>();
        _configurationMock = new Mock<IConfiguration>();
        _messagePublisherMock = new Mock<IMessagePublisher>();
        _emailServiceMock = new Mock<IEmailService>();

        SetupConfiguration();

        _handler = new RegisterCommandHandler(
            _userRepositoryMock.Object,
            _refreshTokenRepositoryMock.Object,
            _auditLogRepositoryMock.Object,
            _passwordHasherMock.Object,
            _tokenServiceMock.Object,
            _mapperMock.Object,
            _configurationMock.Object,
            _messagePublisherMock.Object,
            _emailServiceMock.Object
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
    public async Task Handle_ShouldThrowException_WhenUserAlreadyExists()
    {
        // Arrange
        var command = new RegisterCommand("test@example.com", "Password123!");

        _userRepositoryMock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("User with this email already exists.");
    }

    [Fact]
    public async Task Handle_ShouldCreateUser_WhenEmailIsNew()
    {
        // Arrange
        var command = new RegisterCommand("test@example.com", "Password123!");
        var userId = Guid.NewGuid();
        var passwordHash = "hashedPassword";
        var salt = "salt";
        var accessToken = "access-token";
        var refreshToken = "refresh-token";

        _userRepositoryMock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _passwordHasherMock.Setup(x => x.HashPassword(It.IsAny<string>(), out salt))
            .Returns(passwordHash);

        _userRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(userId);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns(accessToken);

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns(refreshToken);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.RefreshToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto
            {
                Id = userId,
                Email = "test@example.com",
                Role = "Customer"
            });

        _messagePublisherMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _emailServiceMock.Setup(x => x.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.AccessToken.Should().Be(accessToken);
        result.RefreshToken.Should().Be(refreshToken);
        result.User.Email.Should().Be("test@example.com");

        _userRepositoryMock.Verify(x => x.CreateAsync(It.Is<User>(u =>
            u.Email == "test@example.com" &&
            u.Role == "Customer" &&
            u.IsActive == true &&
            u.IsEmailVerified == false
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldHashPassword_BeforeStoringUser()
    {
        // Arrange
        var command = new RegisterCommand("test@example.com", "Password123!");
        var passwordHash = "hashedPassword";
        var salt = "salt";

        _userRepositoryMock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _passwordHasherMock.Setup(x => x.HashPassword(command.Password, out salt))
            .Returns(passwordHash);

        _userRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(Guid.NewGuid());

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh-token");

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.RefreshToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto());

        _emailServiceMock.Setup(x => x.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _passwordHasherMock.Verify(x => x.HashPassword(command.Password, out salt), Times.Once);

        _userRepositoryMock.Verify(x => x.CreateAsync(It.Is<User>(u =>
            u.PasswordHash == passwordHash &&
            u.PasswordSalt == salt
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldCreateRefreshToken_WithCorrectExpiry()
    {
        // Arrange
        var command = new RegisterCommand("test@example.com", "Password123!");
        var userId = Guid.NewGuid();
        var refreshToken = "refresh-token";
        var salt = "salt";

        _userRepositoryMock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _passwordHasherMock.Setup(x => x.HashPassword(It.IsAny<string>(), out salt))
            .Returns("hash");

        _userRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(userId);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns(refreshToken);

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.RefreshToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto());

        _emailServiceMock.Setup(x => x.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _refreshTokenRepositoryMock.Verify(x => x.CreateAsync(It.Is<Domain.Entities.RefreshToken>(rt =>
            rt.UserId == userId &&
            rt.Token == refreshToken &&
            rt.ExpiresAt > DateTime.UtcNow &&
            rt.ExpiresAt <= DateTime.UtcNow.AddDays(7).AddSeconds(5)
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLogAuditEntry_WhenRegistrationSucceeds()
    {
        // Arrange
        var command = new RegisterCommand("test@example.com", "Password123!");
        var userId = Guid.NewGuid();
        var salt = "salt";

        _userRepositoryMock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _passwordHasherMock.Setup(x => x.HashPassword(It.IsAny<string>(), out salt))
            .Returns("hash");

        _userRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(userId);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh-token");

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.RefreshToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _emailServiceMock.Setup(x => x.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto());

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _auditLogRepositoryMock.Verify(x => x.CreateAsync(It.Is<AuditLog>(log =>
            log.UserId == userId &&
            log.Action == "Register" &&
            log.Result == "Success"
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPublishUserRegisteredEvent_WhenRegistrationSucceeds()
    {
        // Arrange
        var command = new RegisterCommand("test@example.com", "Password123!");
        var userId = Guid.NewGuid();
        var salt = "salt";

        _userRepositoryMock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _passwordHasherMock.Setup(x => x.HashPassword(It.IsAny<string>(), out salt))
            .Returns("hash");

        _userRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(userId);

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh-token");

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.RefreshToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto());

        _messagePublisherMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        _emailServiceMock.Setup(x => x.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _messagePublisherMock.Verify(x => x.PublishAsync(
            It.Is<object>(e => e.GetType().Name == "UserRegisteredEvent"),
            "user.registered"
        ), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldNormalizeEmail_ToLowerCase()
    {
        // Arrange
        var command = new RegisterCommand("Test@Example.COM", "Password123!");
        var salt = "salt";

        _userRepositoryMock.Setup(x => x.ExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _passwordHasherMock.Setup(x => x.HashPassword(It.IsAny<string>(), out salt))
            .Returns("hash");

        _userRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<User>()))
            .ReturnsAsync(Guid.NewGuid());

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");

        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("refresh-token");

        _refreshTokenRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<Domain.Entities.RefreshToken>()))
            .ReturnsAsync(Guid.NewGuid());

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _mapperMock.Setup(x => x.Map<UserDto>(It.IsAny<User>()))
            .Returns(new UserDto());

        _emailServiceMock.Setup(x => x.SendEmailVerificationAsync(It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _userRepositoryMock.Verify(x => x.CreateAsync(It.Is<User>(u =>
            u.Email == "test@example.com"
        )), Times.Once);
    }
}
