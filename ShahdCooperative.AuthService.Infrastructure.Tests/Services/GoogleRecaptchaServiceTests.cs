using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ShahdCooperative.AuthService.Infrastructure.Services;
using System.Net;
using System.Text.Json;

namespace ShahdCooperative.AuthService.Infrastructure.Tests.Services;

public class GoogleRecaptchaServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<IConfiguration> _configurationMock;
    private readonly Mock<ILogger<GoogleRecaptchaService>> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;

    public GoogleRecaptchaServiceTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _configurationMock = new Mock<IConfiguration>();
        _loggerMock = new Mock<ILogger<GoogleRecaptchaService>>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();

        // Setup default configuration - must mock IConfigurationSection
        SetupConfigValue("GoogleRecaptcha:Enabled", "true");
        SetupConfigValue("GoogleRecaptcha:SecretKey", "test-secret-key");
        SetupConfigValue("GoogleRecaptcha:MinimumScore", "0.5");
    }

    private void SetupConfigValue(string key, string value)
    {
        var sectionMock = new Mock<IConfigurationSection>();
        sectionMock.Setup(s => s.Value).Returns(value);
        _configurationMock.Setup(c => c.GetSection(key)).Returns(sectionMock.Object);
        _configurationMock.Setup(c => c[key]).Returns(value);
    }

    private HttpClient CreateMockHttpClient(HttpResponseMessage response)
    {
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(response);

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        return httpClient;
    }

    [Fact]
    public async Task VerifyTokenAsync_WhenCaptchaDisabled_ShouldReturnTrue()
    {
        // Arrange
        SetupConfigValue("GoogleRecaptcha:Enabled", "false");

        var service = new GoogleRecaptchaService(_httpClientFactoryMock.Object, _configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await service.VerifyTokenAsync("any-token");

        // Assert
        result.Should().BeTrue();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("CAPTCHA verification is disabled")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyTokenAsync_WhenTokenIsNull_ShouldReturnFalse()
    {
        // Arrange
        var service = new GoogleRecaptchaService(_httpClientFactoryMock.Object, _configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await service.VerifyTokenAsync(null!);

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("CAPTCHA token is null or empty")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyTokenAsync_WhenTokenIsEmpty_ShouldReturnFalse()
    {
        // Arrange
        var service = new GoogleRecaptchaService(_httpClientFactoryMock.Object, _configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await service.VerifyTokenAsync("");

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("CAPTCHA token is null or empty")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyTokenAsync_WhenSecretKeyNotConfigured_ShouldReturnFalse()
    {
        // Arrange
        SetupConfigValue("GoogleRecaptcha:SecretKey", null!);

        var service = new GoogleRecaptchaService(_httpClientFactoryMock.Object, _configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await service.VerifyTokenAsync("test-token");

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("secret key is not configured")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyTokenAsync_WhenApiRequestFails_ShouldReturnFalse()
    {
        // Arrange
        var responseMessage = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        CreateMockHttpClient(responseMessage);

        var service = new GoogleRecaptchaService(_httpClientFactoryMock.Object, _configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await service.VerifyTokenAsync("test-token");

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("reCAPTCHA API request failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyTokenAsync_WhenResponseIsInvalidJson_ShouldReturnFalse()
    {
        // Arrange
        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("invalid json")
        };
        CreateMockHttpClient(responseMessage);

        var service = new GoogleRecaptchaService(_httpClientFactoryMock.Object, _configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await service.VerifyTokenAsync("test-token");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task VerifyTokenAsync_WhenRecaptchaVerificationFails_ShouldReturnFalse()
    {
        // Arrange
        var recaptchaResponse = new
        {
            success = false,
            error_codes = new[] { "invalid-input-response" }
        };

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(recaptchaResponse))
        };
        CreateMockHttpClient(responseMessage);

        var service = new GoogleRecaptchaService(_httpClientFactoryMock.Object, _configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await service.VerifyTokenAsync("test-token");

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("reCAPTCHA verification failed")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyTokenAsync_WhenScoreBelowMinimum_ShouldReturnFalse()
    {
        // Arrange
        var recaptchaResponse = new
        {
            success = true,
            score = 0.3,
            action = "login",
            challenge_ts = "2024-01-01T00:00:00Z",
            hostname = "localhost"
        };

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(recaptchaResponse))
        };
        CreateMockHttpClient(responseMessage);

        var service = new GoogleRecaptchaService(_httpClientFactoryMock.Object, _configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await service.VerifyTokenAsync("test-token");

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("reCAPTCHA score") && o.ToString()!.Contains("below minimum")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyTokenAsync_WhenScoreAboveMinimum_ShouldReturnTrue()
    {
        // Arrange
        var recaptchaResponse = new
        {
            success = true,
            score = 0.8,
            action = "login",
            challenge_ts = "2024-01-01T00:00:00Z",
            hostname = "localhost"
        };

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(recaptchaResponse))
        };
        CreateMockHttpClient(responseMessage);

        var service = new GoogleRecaptchaService(_httpClientFactoryMock.Object, _configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await service.VerifyTokenAsync("test-token");

        // Assert
        result.Should().BeTrue();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("reCAPTCHA verification successful")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task VerifyTokenAsync_WhenScoreIsNull_ShouldReturnTrue()
    {
        // Arrange - reCAPTCHA v2 doesn't return a score
        var recaptchaResponse = new
        {
            success = true,
            challenge_ts = "2024-01-01T00:00:00Z",
            hostname = "localhost"
        };

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(recaptchaResponse))
        };
        CreateMockHttpClient(responseMessage);

        var service = new GoogleRecaptchaService(_httpClientFactoryMock.Object, _configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await service.VerifyTokenAsync("test-token");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task VerifyTokenAsync_WithIpAddress_ShouldIncludeInRequest()
    {
        // Arrange
        var recaptchaResponse = new
        {
            success = true,
            score = 0.9,
            action = "login",
            challenge_ts = "2024-01-01T00:00:00Z",
            hostname = "localhost"
        };

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(recaptchaResponse))
        };
        CreateMockHttpClient(responseMessage);

        var service = new GoogleRecaptchaService(_httpClientFactoryMock.Object, _configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await service.VerifyTokenAsync("test-token", "192.168.1.1");

        // Assert
        result.Should().BeTrue();

        // Verify the HTTP request was made
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.RequestUri!.ToString() == "https://www.google.com/recaptcha/api/siteverify"
            ),
            ItExpr.IsAny<CancellationToken>()
        );
    }

    [Fact]
    public async Task VerifyTokenAsync_WhenExceptionThrown_ShouldReturnFalseAndLogError()
    {
        // Arrange
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ThrowsAsync(new HttpRequestException("Network error"));

        var httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _httpClientFactoryMock.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var service = new GoogleRecaptchaService(_httpClientFactoryMock.Object, _configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await service.VerifyTokenAsync("test-token");

        // Assert
        result.Should().BeFalse();
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Exception occurred during reCAPTCHA verification")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Theory]
    [InlineData(0.5, 0.5, true)]
    [InlineData(0.6, 0.5, true)]
    [InlineData(0.49, 0.5, false)]
    [InlineData(0.9, 0.8, true)]
    [InlineData(0.79, 0.8, false)]
    public async Task VerifyTokenAsync_WithDifferentScores_ShouldRespectMinimumScore(double actualScore, double minimumScore, bool expectedResult)
    {
        // Arrange
        SetupConfigValue("GoogleRecaptcha:MinimumScore", minimumScore.ToString());

        var recaptchaResponse = new
        {
            success = true,
            score = actualScore,
            action = "login",
            challenge_ts = "2024-01-01T00:00:00Z",
            hostname = "localhost"
        };

        var responseMessage = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(recaptchaResponse))
        };
        CreateMockHttpClient(responseMessage);

        var service = new GoogleRecaptchaService(_httpClientFactoryMock.Object, _configurationMock.Object, _loggerMock.Object);

        // Act
        var result = await service.VerifyTokenAsync("test-token");

        // Assert
        result.Should().Be(expectedResult);
    }
}
