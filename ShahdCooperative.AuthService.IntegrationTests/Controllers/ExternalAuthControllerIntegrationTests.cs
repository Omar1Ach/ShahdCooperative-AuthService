using System.Net;
using FluentAssertions;

namespace ShahdCooperative.AuthService.IntegrationTests.Controllers;

public class ExternalAuthControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ExternalAuthControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task LoginWithGoogle_ReturnsRedirectOrChallenge()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/external/google");

        // Assert
        // OAuth endpoints typically return a redirect or challenge response
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect,
            HttpStatusCode.Found,
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task LoginWithFacebook_ReturnsRedirectOrChallenge()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/external/facebook");

        // Assert
        // OAuth endpoints typically return a redirect or challenge response
        response.StatusCode.Should().BeOneOf(
            HttpStatusCode.Redirect,
            HttpStatusCode.Found,
            HttpStatusCode.OK,
            HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GoogleCallback_WithoutAuthentication_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/external/google/callback");

        // Assert
        // Without proper OAuth flow, this should fail
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task FacebookCallback_WithoutAuthentication_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/external/facebook/callback");

        // Assert
        // Without proper OAuth flow, this should fail
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetExternalLogins_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.GetAsync("/api/auth/external/list");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task UnlinkExternalLogin_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await _client.DeleteAsync("/api/auth/external/unlink/google");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
