using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShahdCooperative.AuthService.API.Middleware;
using ShahdCooperative.AuthService.Application.Commands.Disable2FA;
using ShahdCooperative.AuthService.Application.Commands.Enable2FA;
using ShahdCooperative.AuthService.Application.Commands.Verify2FACode;
using ShahdCooperative.AuthService.Application.Commands.Verify2FASetup;
using ShahdCooperative.AuthService.Application.DTOs;

namespace ShahdCooperative.AuthService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TwoFactorController : ControllerBase
{
    private readonly IMediator _mediator;

    public TwoFactorController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Enable two-factor authentication for a user
    /// </summary>
    [HttpPost("enable")]
    [RateLimit("api")]
    [ProducesResponseType(typeof(Enable2FAResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Enable([FromBody] Enable2FARequest request)
    {
        var command = new Enable2FACommand(request.UserId);
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>
    /// Verify 2FA setup by providing a code from authenticator app
    /// </summary>
    [HttpPost("verify-setup")]
    [RateLimit("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifySetup([FromBody] Verify2FASetupRequest request)
    {
        var command = new Verify2FASetupCommand(request.UserId, request.Code);
        var result = await _mediator.Send(command);

        if (result)
        {
            return Ok(new { message = "Two-factor authentication has been successfully enabled" });
        }

        return BadRequest(new { message = "Invalid verification code" });
    }

    /// <summary>
    /// Verify 2FA code during login
    /// </summary>
    [HttpPost("verify")]
    [RateLimit("auth")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> VerifyCode([FromBody] Verify2FACodeRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

        var command = new Verify2FACodeCommand(
            request.Email,
            request.Code,
            request.UseBackupCode,
            ipAddress,
            userAgent
        );

        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>
    /// Disable two-factor authentication
    /// </summary>
    [HttpPost("disable")]
    [RateLimit("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Disable([FromBody] Disable2FARequest request)
    {
        var command = new Disable2FACommand(request.UserId, request.Password);
        var result = await _mediator.Send(command);

        if (result)
        {
            return Ok(new { message = "Two-factor authentication has been disabled" });
        }

        return BadRequest(new { message = "Failed to disable two-factor authentication" });
    }
}
