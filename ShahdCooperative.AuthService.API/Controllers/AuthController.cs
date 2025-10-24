using MediatR;
using Microsoft.AspNetCore.Mvc;
using ShahdCooperative.AuthService.API.Middleware;
using ShahdCooperative.AuthService.Application.Commands.ChangePassword;
using ShahdCooperative.AuthService.Application.Commands.ForgotPassword;
using ShahdCooperative.AuthService.Application.Commands.Login;
using ShahdCooperative.AuthService.Application.Commands.Logout;
using ShahdCooperative.AuthService.Application.Commands.RefreshToken;
using ShahdCooperative.AuthService.Application.Commands.Register;
using ShahdCooperative.AuthService.Application.Commands.ResetPassword;
using ShahdCooperative.AuthService.Application.Commands.VerifyEmail;
using ShahdCooperative.AuthService.Application.DTOs;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICaptchaService _captchaService;

    public AuthController(IMediator mediator, ICaptchaService captchaService)
    {
        _mediator = mediator;
        _captchaService = captchaService;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    [HttpPost("register")]
    [RateLimit("auth")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // Verify CAPTCHA token
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var isCaptchaValid = await _captchaService.VerifyTokenAsync(request.CaptchaToken ?? string.Empty, ipAddress);

        if (!isCaptchaValid)
        {
            return BadRequest(new { error = "CAPTCHA verification failed. Please try again." });
        }

        var command = new RegisterCommand(request.Email, request.Password);
        var response = await _mediator.Send(command);
        return CreatedAtAction(nameof(Register), response);
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    [HttpPost("login")]
    [RateLimit("auth")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();

        // Verify CAPTCHA token
        var isCaptchaValid = await _captchaService.VerifyTokenAsync(request.CaptchaToken ?? string.Empty, ipAddress);

        if (!isCaptchaValid)
        {
            return BadRequest(new { error = "CAPTCHA verification failed. Please try again." });
        }

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

    /// <summary>
    /// Verify email address with verification token
    /// </summary>
    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var command = new VerifyEmailCommand(request.Token);
        var result = await _mediator.Send(command);

        if (result)
        {
            return Ok(new { message = "Email verified successfully" });
        }

        return BadRequest(new { message = "Invalid or expired verification token" });
    }

    /// <summary>
    /// Request password reset token
    /// </summary>
    [HttpPost("forgot-password")]
    [RateLimit("auth")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        var command = new ForgotPasswordCommand(request.Email);
        await _mediator.Send(command);

        // Always return success to prevent email enumeration
        return Ok(new { message = "If the email exists, a password reset link has been sent" });
    }

    /// <summary>
    /// Reset password using reset token
    /// </summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
    {
        var command = new ResetPasswordCommand(request.Token, request.NewPassword);
        var result = await _mediator.Send(command);

        if (result)
        {
            return Ok(new { message = "Password reset successfully" });
        }

        return BadRequest(new { message = "Invalid or expired reset token" });
    }

    /// <summary>
    /// Change password for authenticated user
    /// </summary>
    [HttpPost("change-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        // TODO: Get UserId from JWT claims when authentication is added
        // var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

        var command = new ChangePasswordCommand(request.UserId, request.CurrentPassword, request.NewPassword);
        var result = await _mediator.Send(command);

        if (result)
        {
            return Ok(new { message = "Password changed successfully" });
        }

        return BadRequest(new { message = "Failed to change password" });
    }
}
