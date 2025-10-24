using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShahdCooperative.AuthService.API.Middleware;
using ShahdCooperative.AuthService.Application.Commands.ExternalLogin;
using ShahdCooperative.AuthService.Application.DTOs;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.API.Controllers;

[ApiController]
[Route("api/auth/external")]
public class ExternalAuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IExternalLoginRepository _externalLoginRepository;
    private readonly IConfiguration _configuration;

    public ExternalAuthController(
        IMediator mediator,
        IExternalLoginRepository externalLoginRepository,
        IConfiguration configuration)
    {
        _mediator = mediator;
        _externalLoginRepository = externalLoginRepository;
        _configuration = configuration;
    }

    /// <summary>
    /// Initiate Google OAuth login flow
    /// </summary>
    [HttpGet("google")]
    [RateLimit("auth")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult LoginWithGoogle()
    {
        var redirectUrl = Url.Action(nameof(GoogleCallback), "ExternalAuth");
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Google OAuth callback endpoint
    /// </summary>
    [HttpGet("google/callback")]
    [RateLimit("auth")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GoogleCallback()
    {
        var authenticateResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

        if (!authenticateResult.Succeeded)
        {
            return BadRequest(new { error = "External authentication failed" });
        }

        var claims = authenticateResult.Principal?.Claims;
        var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        var providerKey = claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(providerKey))
        {
            return BadRequest(new { error = "Unable to retrieve user information from Google" });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

        var command = new ExternalLoginCommand(
            Provider: "Google",
            ProviderKey: providerKey,
            Email: email,
            ProviderDisplayName: name,
            IpAddress: ipAddress,
            UserAgent: userAgent
        );

        var response = await _mediator.Send(command);

        // Redirect to frontend with tokens
        var frontendUrl = _configuration["AppSettings:FrontendUrl"];
        return Redirect($"{frontendUrl}/auth/callback?accessToken={response.AccessToken}&refreshToken={response.RefreshToken}");
    }

    /// <summary>
    /// Initiate Facebook OAuth login flow
    /// </summary>
    [HttpGet("facebook")]
    [RateLimit("auth")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult LoginWithFacebook()
    {
        var redirectUrl = Url.Action(nameof(FacebookCallback), "ExternalAuth");
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        return Challenge(properties, FacebookDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Facebook OAuth callback endpoint
    /// </summary>
    [HttpGet("facebook/callback")]
    [RateLimit("auth")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> FacebookCallback()
    {
        var authenticateResult = await HttpContext.AuthenticateAsync(FacebookDefaults.AuthenticationScheme);

        if (!authenticateResult.Succeeded)
        {
            return BadRequest(new { error = "External authentication failed" });
        }

        var claims = authenticateResult.Principal?.Claims;
        var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        var providerKey = claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(providerKey))
        {
            return BadRequest(new { error = "Unable to retrieve user information from Facebook" });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

        var command = new ExternalLoginCommand(
            Provider: "Facebook",
            ProviderKey: providerKey,
            Email: email,
            ProviderDisplayName: name,
            IpAddress: ipAddress,
            UserAgent: userAgent
        );

        var response = await _mediator.Send(command);

        // Redirect to frontend with tokens
        var frontendUrl = _configuration["AppSettings:FrontendUrl"];
        return Redirect($"{frontendUrl}/auth/callback?accessToken={response.AccessToken}&refreshToken={response.RefreshToken}");
    }

    /// <summary>
    /// Get all external logins for the current user
    /// </summary>
    [HttpGet("list")]
    [Authorize]
    [RateLimit("api")]
    [ProducesResponseType(typeof(List<ExternalLoginDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetExternalLogins()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { error = "Invalid user ID" });
        }

        var externalLogins = await _externalLoginRepository.GetByUserIdAsync(userId);

        var dtos = externalLogins.Select(el => new ExternalLoginDto
        {
            Provider = el.Provider,
            ProviderDisplayName = el.ProviderDisplayName,
            Email = el.Email,
            LastLoginAt = el.LastLoginAt,
            CreatedAt = el.CreatedAt
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// Unlink an external login from the current user
    /// </summary>
    [HttpDelete("unlink/{provider}")]
    [Authorize]
    [RateLimit("api")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UnlinkExternalLogin(string provider)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { error = "Invalid user ID" });
        }

        var externalLogins = await _externalLoginRepository.GetByUserIdAsync(userId);
        var externalLogin = externalLogins.FirstOrDefault(el =>
            el.Provider.Equals(provider, StringComparison.OrdinalIgnoreCase));

        if (externalLogin == null)
        {
            return BadRequest(new { error = $"External login with provider '{provider}' not found" });
        }

        await _externalLoginRepository.DeleteAsync(externalLogin.Id);

        return Ok(new { message = $"Successfully unlinked {provider} account" });
    }
}
