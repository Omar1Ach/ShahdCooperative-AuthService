using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShahdCooperative.AuthService.Application.Commands.Login;
using ShahdCooperative.AuthService.Application.Commands.Logout;
using ShahdCooperative.AuthService.Application.Commands.RefreshToken;
using ShahdCooperative.AuthService.Application.Commands.Register;
using ShahdCooperative.AuthService.Application.DTOs;

namespace ShahdCooperative.AuthService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var command = new RegisterCommand(request.Email, request.Password);
        var response = await _mediator.Send(command);
        return CreatedAtAction(nameof(Register), response);
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = HttpContext.Request.Headers.UserAgent.ToString();

        var command = new LoginCommand(request.Email, request.Password, ipAddress, userAgent);
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        var command = new RefreshTokenCommand(request.RefreshToken);
        var response = await _mediator.Send(command);
        return Ok(response);
    }

    /// <summary>
    /// Logout and revoke refresh token
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest request)
    {
        var command = new LogoutCommand(request.RefreshToken);
        var result = await _mediator.Send(command);

        if (result)
        {
            return Ok(new { message = "Logged out successfully" });
        }

        return BadRequest(new { message = "Invalid refresh token" });
    }
}
