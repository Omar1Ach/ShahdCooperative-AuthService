using Microsoft.Extensions.Caching.Memory;
using System.Net;

namespace ShahdCooperative.AuthService.API.Middleware;

public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IMemoryCache _cache;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private readonly IConfiguration _configuration;

    public RateLimitingMiddleware(
        RequestDelegate next,
        IMemoryCache cache,
        ILogger<RateLimitingMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _cache = cache;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var endpoint = context.GetEndpoint();
        var rateLimitAttribute = endpoint?.Metadata.GetMetadata<RateLimitAttribute>();

        if (rateLimitAttribute != null)
        {
            var ipAddress = GetClientIpAddress(context);
            var cacheKey = $"RateLimit_{rateLimitAttribute.Policy}_{ipAddress}";

            if (!_cache.TryGetValue(cacheKey, out int requestCount))
            {
                requestCount = 0;
            }

            requestCount++;

            var limit = GetLimitForPolicy(rateLimitAttribute.Policy);
            var windowMinutes = GetWindowForPolicy(rateLimitAttribute.Policy);

            if (requestCount > limit)
            {
                _logger.LogWarning("Rate limit exceeded for IP {IpAddress} on policy {Policy}", ipAddress, rateLimitAttribute.Policy);

                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.ContentType = "application/json";

                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Too many requests",
                    message = $"Rate limit exceeded. Please try again in {windowMinutes} minute(s).",
                    retryAfter = windowMinutes * 60
                });

                return;
            }

            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(windowMinutes)
            };

            _cache.Set(cacheKey, requestCount, cacheOptions);
        }

        await _next(context);
    }

    private string GetClientIpAddress(HttpContext context)
    {
        var ipAddress = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();

        if (string.IsNullOrEmpty(ipAddress))
        {
            ipAddress = context.Connection.RemoteIpAddress?.ToString();
        }

        return ipAddress ?? "unknown";
    }

    private int GetLimitForPolicy(string policy)
    {
        return policy switch
        {
            "auth" => _configuration.GetValue<int>("RateLimiting:Auth:Limit", 5),
            "api" => _configuration.GetValue<int>("RateLimiting:Api:Limit", 100),
            "admin" => _configuration.GetValue<int>("RateLimiting:Admin:Limit", 50),
            _ => 100
        };
    }

    private int GetWindowForPolicy(string policy)
    {
        return policy switch
        {
            "auth" => _configuration.GetValue<int>("RateLimiting:Auth:WindowMinutes", 15),
            "api" => _configuration.GetValue<int>("RateLimiting:Api:WindowMinutes", 1),
            "admin" => _configuration.GetValue<int>("RateLimiting:Admin:WindowMinutes", 5),
            _ => 1
        };
    }
}

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RateLimitAttribute : Attribute
{
    public string Policy { get; }

    public RateLimitAttribute(string policy)
    {
        Policy = policy;
    }
}
