using FluentAssertions;
using ShahdCooperative.AuthService.Infrastructure.Services;

namespace ShahdCooperative.AuthService.Infrastructure.Tests;

public class PasswordHasherTests
{
    private readonly PasswordHasher _passwordHasher;

    public PasswordHasherTests()
    {
        _passwordHasher = new PasswordHasher();
    }

    [Fact]
    public void HashPassword_ShouldReturnHashAndSalt()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash = _passwordHasher.HashPassword(password, out var salt);

        // Assert
        hash.Should().NotBeNullOrEmpty();
        salt.Should().NotBeNullOrEmpty();
        hash.Should().NotBe(password);
    }

    [Fact]
    public void HashPassword_ShouldGenerateDifferentHashesForSamePassword()
    {
        // Arrange
        var password = "TestPassword123!";

        // Act
        var hash1 = _passwordHasher.HashPassword(password, out var salt1);
        var hash2 = _passwordHasher.HashPassword(password, out var salt2);

        // Assert
        hash1.Should().NotBe(hash2);
        salt1.Should().NotBe(salt2);
    }

    [Fact]
    public void VerifyPassword_ShouldReturnTrue_WhenPasswordIsCorrect()
    {
        // Arrange
        var password = "TestPassword123!";
        var hash = _passwordHasher.HashPassword(password, out var salt);

        // Act
        var result = _passwordHasher.VerifyPassword(password, hash, salt);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalse_WhenPasswordIsIncorrect()
    {
        // Arrange
        var password = "TestPassword123!";
        var wrongPassword = "WrongPassword456!";
        var hash = _passwordHasher.HashPassword(password, out var salt);

        // Act
        var result = _passwordHasher.VerifyPassword(wrongPassword, hash, salt);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyPassword_ShouldReturnFalse_WhenHashIsWrong()
    {
        // Arrange
        var password = "TestPassword123!";
        _passwordHasher.HashPassword(password, out var salt);
        var wrongHash = "$2a$12$wronghashwronghashwronghashwronghashwronghashwronghash";

        // Act
        var result = _passwordHasher.VerifyPassword(password, wrongHash, salt);

        // Assert
        result.Should().BeFalse();
    }
}
