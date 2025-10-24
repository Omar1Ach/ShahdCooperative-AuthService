using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ShahdCooperative.AuthService.API.Middleware;
using System.Net;

namespace ShahdCooperative.AuthService.API.Tests.Middleware;

public class RateLimitingMiddlewareTests
{
    private readonly Mock<ILogger<RateLimitingMiddleware>> _loggerMock;
    private readonly IMemoryCache _cache;
    private readonly Mock<IConfiguration> _configurationMock;

    public RateLimitingMiddlewareTests()
    {
        _loggerMock = new Mock<ILogger<RateLimitingMiddleware>>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _configurationMock = new Mock<IConfiguration>();

        // Setup default configuration values
        SetupConfiguration("auth", 5, 15);
        SetupConfiguration("api", 100, 1);
        SetupConfiguration("admin", 50, 5);
    }

    private void SetupConfiguration(string policy, int limit, int windowMinutes)
    {
        var policyUpper = char.ToUpper(policy[0]) + policy.Substring(1);

        // Mock GetSection for Limit
        var limitSectionMock = new Mock<IConfigurationSection>();
        limitSectionMock.Setup(s => s.Value).Returns(limit.ToString());
        _configurationMock
            .Setup(c => c.GetSection($"RateLimiting:{policyUpper}:Limit"))
            .Returns(limitSectionMock.Object);

        // Mock GetSection for WindowMinutes
        var windowSectionMock = new Mock<IConfigurationSection>();
        windowSectionMock.Setup(s => s.Value).Returns(windowMinutes.ToString());
        _configurationMock
            .Setup(c => c.GetSection($"RateLimiting:{policyUpper}:WindowMinutes"))
            .Returns(windowSectionMock.Object);
    }

    [Fact]
    public async Task InvokeAsync_WhenNoRateLimitAttribute_ShouldCallNextMiddleware()
    {
        // Arrange
        var context = new DefaultHttpContext();
        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new RateLimitingMiddleware(next, _cache, _loggerMock.Object, _configurationMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
    }

    [Fact]
    public async Task InvokeAsync_FirstRequest_ShouldAllowAndCallNext()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

        var endpoint = new Endpoint(
            (ctx) => Task.CompletedTask,
            new EndpointMetadataCollection(new RateLimitAttribute("auth")),
            "test");
        context.SetEndpoint(endpoint);

        var nextCalled = false;
        RequestDelegate next = (ctx) =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        };

        var middleware = new RateLimitingMiddleware(next, _cache, _loggerMock.Object, _configurationMock.Object);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        nextCalled.Should().BeTrue();
        context.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task InvokeAsync_WithinLimit_ShouldAllowAllRequests()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.2");

        var endpoint = new Endpoint(
            (ctx) => Task.CompletedTask,
            new EndpointMetadataCollection(new RateLimitAttribute("auth")),
            "test");
        context.SetEndpoint(endpoint);

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new RateLimitingMiddleware(next, _cache, _loggerMock.Object, _configurationMock.Object);

        // Act - Make 5 requests (within limit)
        for (int i = 0; i < 5; i++)
        {
            context.Response.Body = new MemoryStream();
            await middleware.InvokeAsync(context);
            context.Response.StatusCode.Should().Be(200, $"Request {i + 1} should succeed");
        }
    }

    [Fact]
    public async Task InvokeAsync_ExceedingLimit_ShouldReturn429()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.3");
        context.Response.Body = new MemoryStream();

        var endpoint = new Endpoint(
            (ctx) => Task.CompletedTask,
            new EndpointMetadataCollection(new RateLimitAttribute("auth")),
            "test");
        context.SetEndpoint(endpoint);

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new RateLimitingMiddleware(next, _cache, _loggerMock.Object, _configurationMock.Object);

        // Act - Make 5 requests (at limit)
        for (int i = 0; i < 5; i++)
        {
            context.Response.Body = new MemoryStream();
            await middleware.InvokeAsync(context);
        }

        // Act - Make 6th request (exceeding limit)
        context.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context);

        // Assert
        context.Response.StatusCode.Should().Be((int)HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task InvokeAsync_ExceedingLimit_ShouldReturnErrorMessage()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.4");
        context.Response.Body = new MemoryStream();

        var endpoint = new Endpoint(
            (ctx) => Task.CompletedTask,
            new EndpointMetadataCollection(new RateLimitAttribute("auth")),
            "test");
        context.SetEndpoint(endpoint);

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new RateLimitingMiddleware(next, _cache, _loggerMock.Object, _configurationMock.Object);

        // Act - Exceed limit
        for (int i = 0; i < 6; i++)
        {
            context.Response.Body = new MemoryStream();
            await middleware.InvokeAsync(context);
        }

        // Assert
        context.Response.ContentType.Should().StartWith("application/json");
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        responseBody.Should().Contain("Too many requests");
        responseBody.Should().Contain("Rate limit exceeded");
    }

    [Fact]
    public async Task InvokeAsync_DifferentIpAddresses_ShouldTrackSeparately()
    {
        // Arrange
        var endpoint = new Endpoint(
            (ctx) => Task.CompletedTask,
            new EndpointMetadataCollection(new RateLimitAttribute("auth")),
            "test");

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new RateLimitingMiddleware(next, _cache, _loggerMock.Object, _configurationMock.Object);

        // Act - IP1: Make 5 requests (at limit)
        for (int i = 0; i < 5; i++)
        {
            var context1 = new DefaultHttpContext();
            context1.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.10");
            context1.SetEndpoint(endpoint);
            await middleware.InvokeAsync(context1);
        }

        // Act - IP2: Make 5 requests (at limit, but different IP)
        for (int i = 0; i < 5; i++)
        {
            var context2 = new DefaultHttpContext();
            context2.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.20");
            context2.SetEndpoint(endpoint);
            await middleware.InvokeAsync(context2);
            context2.Response.StatusCode.Should().Be(200, "Different IP should have separate limit");
        }

        // Act - IP1: 6th request should fail
        var context1Final = new DefaultHttpContext();
        context1Final.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.10");
        context1Final.SetEndpoint(endpoint);
        context1Final.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(context1Final);

        // Assert
        context1Final.Response.StatusCode.Should().Be((int)HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task InvokeAsync_DifferentPolicies_ShouldHaveDifferentLimits()
    {
        // Arrange
        var authEndpoint = new Endpoint(
            (ctx) => Task.CompletedTask,
            new EndpointMetadataCollection(new RateLimitAttribute("auth")),
            "test-auth");

        var apiEndpoint = new Endpoint(
            (ctx) => Task.CompletedTask,
            new EndpointMetadataCollection(new RateLimitAttribute("api")),
            "test-api");

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new RateLimitingMiddleware(next, _cache, _loggerMock.Object, _configurationMock.Object);

        // Act - Auth endpoint: Make 5 requests (at auth limit)
        for (int i = 0; i < 5; i++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.30");
            context.SetEndpoint(authEndpoint);
            await middleware.InvokeAsync(context);
        }

        // Act - API endpoint: Should still allow requests (different policy, higher limit)
        for (int i = 0; i < 10; i++)
        {
            var context = new DefaultHttpContext();
            context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.30");
            context.SetEndpoint(apiEndpoint);
            await middleware.InvokeAsync(context);
            context.Response.StatusCode.Should().Be(200, "API policy has higher limit");
        }
    }

    [Fact]
    public async Task InvokeAsync_WithXForwardedForHeader_ShouldUseHeaderIp()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Headers["X-Forwarded-For"] = "10.0.0.50";
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.1");

        var endpoint = new Endpoint(
            (ctx) => Task.CompletedTask,
            new EndpointMetadataCollection(new RateLimitAttribute("auth")),
            "test");
        context.SetEndpoint(endpoint);

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new RateLimitingMiddleware(next, _cache, _loggerMock.Object, _configurationMock.Object);

        // Act - Make 5 requests (at limit)
        for (int i = 0; i < 5; i++)
        {
            var ctx = new DefaultHttpContext();
            ctx.Request.Headers["X-Forwarded-For"] = "10.0.0.50";
            ctx.SetEndpoint(endpoint);
            await middleware.InvokeAsync(ctx);
        }

        // Act - 6th request should be rate limited
        var finalContext = new DefaultHttpContext();
        finalContext.Request.Headers["X-Forwarded-For"] = "10.0.0.50";
        finalContext.SetEndpoint(endpoint);
        finalContext.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(finalContext);

        // Assert
        finalContext.Response.StatusCode.Should().Be((int)HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task InvokeAsync_LogsWarningWhenRateLimitExceeded()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse("192.168.1.100");
        context.Response.Body = new MemoryStream();

        var endpoint = new Endpoint(
            (ctx) => Task.CompletedTask,
            new EndpointMetadataCollection(new RateLimitAttribute("auth")),
            "test");
        context.SetEndpoint(endpoint);

        RequestDelegate next = (ctx) => Task.CompletedTask;
        var middleware = new RateLimitingMiddleware(next, _cache, _loggerMock.Object, _configurationMock.Object);

        // Act - Exceed limit
        for (int i = 0; i < 6; i++)
        {
            context.Response.Body = new MemoryStream();
            await middleware.InvokeAsync(context);
        }

        // Assert - Verify warning was logged
        _loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("Rate limit exceeded")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
