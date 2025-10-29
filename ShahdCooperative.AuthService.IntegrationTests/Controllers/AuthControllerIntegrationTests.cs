using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ShahdCooperative.AuthService.Application.DTOs;

namespace ShahdCooperative.AuthService.IntegrationTests.Controllers;

public class AuthControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AuthControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsCreated()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Email = $"test{Guid.NewGuid()}@example.com",
            Password = "Test123!@#",
            CaptchaToken = "test-captcha-token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse.Should().NotBeNull();
        loginResponse!.AccessToken.Should().NotBeNullOrEmpty();
        loginResponse.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Email = "invalid-email",
            Password = "Test123!@#",
            CaptchaToken = "test-captcha-token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithWeakPassword_ReturnsBadRequest()
    {
        // Arrange
        var registerRequest = new RegisterRequest
        {
            Email = $"test{Guid.NewGuid()}@example.com",
            Password = "weak",
            CaptchaToken = "test-captcha-token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOk()
    {
        // Arrange - First register a user
        var email = $"logintest{Guid.NewGuid()}@example.com";
        var password = "Test123!@#";

        var registerRequest = new RegisterRequest
        {
            Email = email,
            Password = password,
            CaptchaToken = "test-captcha-token"
        };
        await _client.PostAsJsonAsync("/api/auth/register", registerRequest);

        var loginRequest = new LoginRequest
        {
            Email = email,
            Password = password,
            CaptchaToken = "test-captcha-token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        loginResponse.Should().NotBeNull();
        loginResponse!.AccessToken.Should().NotBeNullOrEmpty();
        loginResponse.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsUnauthorized()
    {
        // Arrange
        var loginRequest = new LoginRequest
        {
            Email = "nonexistent@example.com",
            Password = "WrongPassword123!",
            CaptchaToken = "test-captcha-token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/login", loginRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RefreshToken_WithValidToken_ReturnsNewTokens()
    {
        // Arrange - Register and login to get tokens
        var email = $"refresh{Guid.NewGuid()}@example.com";
        var password = "Test123!@#";

        var registerRequest = new RegisterRequest
        {
            Email = email,
            Password = password,
            CaptchaToken = "test-captcha-token"
        };
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var initialLogin = await registerResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = initialLogin!.RefreshToken
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var refreshResponse = await response.Content.ReadFromJsonAsync<LoginResponse>();
        refreshResponse.Should().NotBeNull();
        refreshResponse!.AccessToken.Should().NotBeNullOrEmpty();
        refreshResponse.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task RefreshToken_WithInvalidToken_ReturnsUnauthorized()
    {
        // Arrange
        var refreshRequest = new RefreshTokenRequest
        {
            RefreshToken = "invalid-refresh-token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/refresh", refreshRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithValidRefreshToken_ReturnsOk()
    {
        // Arrange - Register to get refresh token
        var email = $"logout{Guid.NewGuid()}@example.com";
        var registerRequest = new RegisterRequest
        {
            Email = email,
            Password = "Test123!@#",
            CaptchaToken = "test-captcha-token"
        };
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var loginResponse = await registerResponse.Content.ReadFromJsonAsync<LoginResponse>();

        var logoutRequest = new RefreshTokenRequest
        {
            RefreshToken = loginResponse!.RefreshToken
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/logout", logoutRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_WithAnyEmail_ReturnsOk()
    {
        // Arrange
        var request = new ForgotPasswordRequest
        {
            Email = "any@example.com"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/forgot-password", request);

        // Assert
        // Should always return OK to prevent email enumeration
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new VerifyEmailRequest
        {
            Token = "invalid-verification-token"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/verify-email", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_ReturnsBadRequest()
    {
        // Arrange
        var request = new ResetPasswordRequest
        {
            Token = "invalid-reset-token",
            NewPassword = "NewPassword123!@#"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/auth/reset-password", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
