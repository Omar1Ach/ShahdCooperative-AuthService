using FluentAssertions;
using Moq;
using OtpNet;
using ShahdCooperative.AuthService.Domain.Interfaces;
using ShahdCooperative.AuthService.Infrastructure.Services;

namespace ShahdCooperative.AuthService.Infrastructure.Tests.Services;

public class TwoFactorServiceTests
{
    private readonly Mock<IPasswordHasher> _passwordHasherMock;
    private readonly TwoFactorService _service;

    public TwoFactorServiceTests()
    {
        _passwordHasherMock = new Mock<IPasswordHasher>();
        _service = new TwoFactorService(_passwordHasherMock.Object);
    }

    [Fact]
    public void GenerateSecret_ShouldReturnBase32EncodedString()
    {
        // Act
        var secret = _service.GenerateSecret();

        // Assert
        secret.Should().NotBeNullOrWhiteSpace();
        secret.Length.Should().BeGreaterThan(0);

        // Verify it's valid Base32
        var isValidBase32 = IsValidBase32(secret);
        isValidBase32.Should().BeTrue();
    }

    [Fact]
    public void GenerateSecret_ShouldReturnDifferentValues()
    {
        // Act
        var secret1 = _service.GenerateSecret();
        var secret2 = _service.GenerateSecret();

        // Assert
        secret1.Should().NotBe(secret2);
    }

    [Fact]
    public void GenerateQrCodeUri_ShouldReturnValidOtpAuthUri()
    {
        // Arrange
        var email = "test@example.com";
        var secret = _service.GenerateSecret();

        // Act
        var uri = _service.GenerateQrCodeUri(email, secret);

        // Assert
        uri.Should().StartWith("otpauth://totp/");
        uri.Should().Contain("test%40example.com"); // @ is encoded as %40
        uri.Should().Contain(secret);
        uri.Should().Contain("ShahdCooperative");
    }

    [Fact]
    public void GenerateQrCodeUri_WithCustomIssuer_ShouldIncludeIssuer()
    {
        // Arrange
        var email = "test@example.com";
        var secret = _service.GenerateSecret();
        var issuer = "CustomIssuer";

        // Act
        var uri = _service.GenerateQrCodeUri(email, secret, issuer);

        // Assert
        uri.Should().Contain(issuer);
        uri.Should().Contain($"issuer={issuer}");
    }

    [Fact]
    public void GenerateQrCodeUri_ShouldEscapeSpecialCharacters()
    {
        // Arrange
        var email = "test+user@example.com";
        var secret = _service.GenerateSecret();

        // Act
        var uri = _service.GenerateQrCodeUri(email, secret);

        // Assert
        uri.Should().Contain("%2B"); // + should be escaped
    }

    [Fact]
    public void GenerateQrCodeImage_ShouldReturnBase64String()
    {
        // Arrange
        var email = "test@example.com";
        var secret = _service.GenerateSecret();
        var uri = _service.GenerateQrCodeUri(email, secret);

        // Act
        var qrCode = _service.GenerateQrCodeImage(uri);

        // Assert
        qrCode.Should().NotBeNullOrWhiteSpace();

        // Verify it's valid Base64
        var isValidBase64 = IsValidBase64(qrCode);
        isValidBase64.Should().BeTrue();
    }

    [Fact]
    public void VerifyCode_WithValidCode_ShouldReturnTrue()
    {
        // Arrange
        var secret = _service.GenerateSecret();
        var secretBytes = Base32Encoding.ToBytes(secret);
        var totp = new Totp(secretBytes);
        var validCode = totp.ComputeTotp();

        // Act
        var result = _service.VerifyCode(secret, validCode);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyCode_WithInvalidCode_ShouldReturnFalse()
    {
        // Arrange
        var secret = _service.GenerateSecret();
        var invalidCode = "000000";

        // Act
        var result = _service.VerifyCode(secret, invalidCode);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyCode_WithNullSecret_ShouldReturnFalse()
    {
        // Arrange
        var code = "123456";

        // Act
        var result = _service.VerifyCode(null!, code);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyCode_WithEmptySecret_ShouldReturnFalse()
    {
        // Arrange
        var code = "123456";

        // Act
        var result = _service.VerifyCode(string.Empty, code);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyCode_WithNullCode_ShouldReturnFalse()
    {
        // Arrange
        var secret = _service.GenerateSecret();

        // Act
        var result = _service.VerifyCode(secret, null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyCode_WithEmptyCode_ShouldReturnFalse()
    {
        // Arrange
        var secret = _service.GenerateSecret();

        // Act
        var result = _service.VerifyCode(secret, string.Empty);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GenerateBackupCodes_ShouldReturnCorrectCount()
    {
        // Act
        var codes = _service.GenerateBackupCodes(10);

        // Assert
        codes.Should().HaveCount(10);
    }

    [Fact]
    public void GenerateBackupCodes_WithCustomCount_ShouldReturnRequestedCount()
    {
        // Act
        var codes = _service.GenerateBackupCodes(5);

        // Assert
        codes.Should().HaveCount(5);
    }

    [Fact]
    public void GenerateBackupCodes_ShouldReturnUniqueValues()
    {
        // Act
        var codes = _service.GenerateBackupCodes(10);

        // Assert
        codes.Distinct().Should().HaveCount(10);
    }

    [Fact]
    public void GenerateBackupCodes_ShouldReturn8CharacterCodes()
    {
        // Act
        var codes = _service.GenerateBackupCodes(10);

        // Assert
        codes.Should().AllSatisfy(code => code.Length.Should().Be(8));
    }

    [Fact]
    public void GenerateBackupCodes_ShouldOnlyContainValidCharacters()
    {
        // Act
        var codes = _service.GenerateBackupCodes(10);

        // Assert
        var validChars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        codes.Should().AllSatisfy(code =>
        {
            foreach (var ch in code)
            {
                validChars.Should().Contain(ch.ToString());
            }
        });
    }

    [Fact]
    public void HashBackupCode_ShouldReturnHashedValue()
    {
        // Arrange
        var code = "ABC12345";
        var salt = "salt";
        _passwordHasherMock
            .Setup(h => h.HashPassword(code, out salt))
            .Returns("hashed_code");

        // Act
        var hash = _service.HashBackupCode(code);

        // Assert
        hash.Should().Be("hashed_code");
    }

    [Fact]
    public void VerifyBackupCode_WithValidCode_ShouldReturnTrue()
    {
        // Arrange
        var code = "ABC12345";
        var hash = "hashed_code";
        _passwordHasherMock
            .Setup(h => h.VerifyPassword(code, hash, string.Empty))
            .Returns(true);

        // Act
        var result = _service.VerifyBackupCode(code, hash);

        // Assert
        result.Should().BeTrue();
        _passwordHasherMock.Verify(h => h.VerifyPassword(code, hash, string.Empty), Times.Once);
    }

    [Fact]
    public void VerifyBackupCode_WithInvalidCode_ShouldReturnFalse()
    {
        // Arrange
        var code = "ABC12345";
        var hash = "hashed_code";
        _passwordHasherMock
            .Setup(h => h.VerifyPassword(code, hash, string.Empty))
            .Returns(false);

        // Act
        var result = _service.VerifyBackupCode(code, hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyBackupCode_WithNullCode_ShouldReturnFalse()
    {
        // Arrange
        var hash = "hashed_code";

        // Act
        var result = _service.VerifyBackupCode(null!, hash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void VerifyBackupCode_WithNullHash_ShouldReturnFalse()
    {
        // Arrange
        var code = "ABC12345";

        // Act
        var result = _service.VerifyBackupCode(code, null!);

        // Assert
        result.Should().BeFalse();
    }

    private bool IsValidBase32(string input)
    {
        try
        {
            Base32Encoding.ToBytes(input);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsValidBase64(string input)
    {
        try
        {
            Convert.FromBase64String(input);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
