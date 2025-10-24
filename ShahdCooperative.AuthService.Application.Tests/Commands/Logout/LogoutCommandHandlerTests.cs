using FluentAssertions;
using Moq;
using ShahdCooperative.AuthService.Domain.Entities;
using ShahdCooperative.AuthService.Domain.Interfaces;
using LogoutCommand = ShahdCooperative.AuthService.Application.Commands.Logout.LogoutCommand;
using LogoutCommandHandler = ShahdCooperative.AuthService.Application.Commands.Logout.LogoutCommandHandler;

namespace ShahdCooperative.AuthService.Application.Tests.Commands.Logout;

public class LogoutCommandHandlerTests
{
    private readonly Mock<IRefreshTokenRepository> _refreshTokenRepositoryMock;
    private readonly Mock<IAuditLogRepository> _auditLogRepositoryMock;
    private readonly Mock<IMessagePublisher> _messagePublisherMock;
    private readonly LogoutCommandHandler _handler;

    public LogoutCommandHandlerTests()
    {
        _refreshTokenRepositoryMock = new Mock<IRefreshTokenRepository>();
        _auditLogRepositoryMock = new Mock<IAuditLogRepository>();
        _messagePublisherMock = new Mock<IMessagePublisher>();

        _handler = new LogoutCommandHandler(
            _refreshTokenRepositoryMock.Object,
            _auditLogRepositoryMock.Object,
            _messagePublisherMock.Object
        );
    }

    [Fact]
    public async Task Handle_ShouldReturnFalse_WhenRefreshTokenNotFound()
    {
        // Arrange
        var command = new LogoutCommand("invalid-token");

        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(It.IsAny<string>()))
            .ReturnsAsync((Domain.Entities.RefreshToken?)null);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeFalse();

        _refreshTokenRepositoryMock.Verify(x => x.RevokeAsync(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
        _auditLogRepositoryMock.Verify(x => x.CreateAsync(It.IsAny<AuditLog>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ShouldRevokeToken_WhenTokenIsValid()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = "valid-refresh-token";
        var command = new LogoutCommand(token);

        var refreshToken = new Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(token))
            .ReturnsAsync(refreshToken);

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAsync(token, It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _messagePublisherMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();

        _refreshTokenRepositoryMock.Verify(x => x.RevokeAsync(token, It.IsAny<string?>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldLogAuditEntry_WhenLogoutSucceeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = "valid-refresh-token";
        var command = new LogoutCommand(token);

        var refreshToken = new Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(token))
            .ReturnsAsync(refreshToken);

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAsync(token, It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _messagePublisherMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _auditLogRepositoryMock.Verify(x => x.CreateAsync(It.Is<AuditLog>(log =>
            log.UserId == userId &&
            log.Action == "Logout" &&
            log.Result == "Success"
        )), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldPublishUserLoggedOutEvent_WhenLogoutSucceeds()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = "valid-refresh-token";
        var command = new LogoutCommand(token);

        var refreshToken = new Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(token))
            .ReturnsAsync(refreshToken);

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAsync(token, It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _messagePublisherMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _messagePublisherMock.Verify(x => x.PublishAsync(
            It.Is<object>(e => e.GetType().Name == "UserLoggedOutEvent"),
            "user.logged-out"
        ), Times.Once);
    }

    [Fact]
    public async Task Handle_ShouldReturnTrue_EvenWhenEventPublishingFails()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var token = "valid-refresh-token";
        var command = new LogoutCommand(token);

        var refreshToken = new Domain.Entities.RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Token = token,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        };

        _refreshTokenRepositoryMock.Setup(x => x.GetByTokenAsync(token))
            .ReturnsAsync(refreshToken);

        _refreshTokenRepositoryMock.Setup(x => x.RevokeAsync(token, It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        _auditLogRepositoryMock.Setup(x => x.CreateAsync(It.IsAny<AuditLog>()))
            .ReturnsAsync(Guid.NewGuid());

        _messagePublisherMock.Setup(x => x.PublishAsync(It.IsAny<object>(), It.IsAny<string>()))
            .ThrowsAsync(new Exception("RabbitMQ connection failed"));

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().BeTrue();
    }
}
