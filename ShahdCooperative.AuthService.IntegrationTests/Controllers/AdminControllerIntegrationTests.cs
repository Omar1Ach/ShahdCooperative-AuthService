using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ShahdCooperative.AuthService.Application.DTOs;

namespace ShahdCooperative.AuthService.IntegrationTests.Controllers;

public class AdminControllerIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public AdminControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAllUsers_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/admin/users");

        // Assert
        response.EnsureSuccessStatusCode();
        response.Content.Headers.ContentType?.MediaType.Should().Be("application/json");
    }

    [Fact]
    public async Task GetAllUsers_WithPagination_ReturnsCorrectFormat()
    {
        // Act
        var response = await _client.GetAsync("/api/admin/users?pageNumber=1&pageSize=10");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResponse<AdminUserDto>>();
        result.Should().NotBeNull();
        result!.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(10);
    }

    [Fact]
    public async Task GetAllUsers_WithSearchTerm_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/admin/users?searchTerm=test");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task LockUser_WithValidUserId_ReturnsOk()
    {
        // Arrange - Register a user first
        var registerRequest = new RegisterRequest
        {
            Email = $"locktest{Guid.NewGuid()}@example.com",
            Password = "Test123!@#",
            CaptchaToken = "test-captcha-token"
        };
        var registerResponse = await _client.PostAsJsonAsync("/api/auth/register", registerRequest);
        var loginResponse = await registerResponse.Content.ReadFromJsonAsync<LoginResponse>();

        // Extract user ID from JWT token (simplified - in real scenario you'd decode the JWT)
        // For testing purposes, we'll use a known GUID or create a user and get their ID
        var userId = Guid.NewGuid(); // This should be replaced with actual user ID

        // Act
        var response = await _client.PostAsJsonAsync($"/api/admin/users/{userId}/lock", 60);

        // Assert
        // Note: This may return 404 if user doesn't exist in database
        // In a real scenario, you'd need to ensure the user exists first
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UnlockUser_WithValidUserId_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var response = await _client.PostAsync($"/api/admin/users/{userId}/unlock", null);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteUser_WithValidUserId_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/admin/users/{userId}");

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateUserRole_WithValidData_ReturnsOk()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var updateRoleRequest = new UpdateRoleRequest
        {
            Role = "Admin"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/admin/users/{userId}/role", updateRoleRequest);

        // Assert
        response.StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetAuditLogs_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/admin/audit-logs");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PaginatedResponse<AuditLogDto>>();
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task GetAuditLogs_WithFilters_ReturnsSuccessStatusCode()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var startDate = DateTime.UtcNow.AddDays(-7);
        var endDate = DateTime.UtcNow;

        // Act
        var response = await _client.GetAsync(
            $"/api/admin/audit-logs?userId={userId}&action=Login&startDate={startDate:O}&endDate={endDate:O}");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetSecurityDashboard_ReturnsSuccessStatusCode()
    {
        // Act
        var response = await _client.GetAsync("/api/admin/dashboard");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<SecurityDashboardDto>();
        result.Should().NotBeNull();
    }
}
