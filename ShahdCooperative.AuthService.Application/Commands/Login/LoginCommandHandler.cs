using AutoMapper;
using MediatR;
using Microsoft.Extensions.Configuration;
using ShahdCooperative.AuthService.Application.DTOs;
using ShahdCooperative.AuthService.Domain.Entities;
using ShahdCooperative.AuthService.Domain.Events;
using ShahdCooperative.AuthService.Domain.Exceptions;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Application.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;
    private readonly IMessagePublisher _messagePublisher;

    private const int MaxFailedAttempts = 5;
    private const int LockoutMinutes = 15;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IAuditLogRepository auditLogRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IMapper mapper,
        IConfiguration configuration,
        IMessagePublisher messagePublisher)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _auditLogRepository = auditLogRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _mapper = mapper;
        _configuration = configuration;
        _messagePublisher = messagePublisher;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        // Get user by email
        var user = await _userRepository.GetByEmailAsync(request.Email.ToLowerInvariant());

        if (user == null)
        {
            await LogFailedAttempt(null, request.Email, request.IpAddress, request.UserAgent, "User not found");
            throw new InvalidCredentialsException();
        }

        // Check if account is locked
        if (user.LockoutEnd.HasValue && user.LockoutEnd.Value > DateTime.UtcNow)
        {
            await LogFailedAttempt(user.Id, request.Email, request.IpAddress, request.UserAgent, "Account locked");
            throw new AccountLockedException(user.LockoutEnd.Value);
        }

        // Check if account is active
        if (!user.IsActive)
        {
            await LogFailedAttempt(user.Id, request.Email, request.IpAddress, request.UserAgent, "Account inactive");
            throw new InvalidCredentialsException("Account is inactive.");
        }

        // Verify password
        if (!_passwordHasher.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            // Increment failed attempts
            await _userRepository.IncrementFailedLoginAttemptsAsync(user.Id);

            // Get updated user to check failed attempts
            var updatedUser = await _userRepository.GetByIdAsync(user.Id);

            if (updatedUser != null && updatedUser.FailedLoginAttempts >= MaxFailedAttempts)
            {
                var lockoutEnd = DateTime.UtcNow.AddMinutes(LockoutMinutes);
                await _userRepository.SetLockoutEndAsync(updatedUser.Id, lockoutEnd);
                await LogFailedAttempt(updatedUser.Id, request.Email, request.IpAddress, request.UserAgent, $"Account locked after {MaxFailedAttempts} failed attempts");
                throw new AccountLockedException(lockoutEnd);
            }

            await LogFailedAttempt(user.Id, request.Email, request.IpAddress, request.UserAgent, "Invalid password");
            throw new InvalidCredentialsException();
        }

        // Reset failed login attempts
        await _userRepository.ResetFailedLoginAttemptsAsync(user.Id);

        // Update last login
        await _userRepository.UpdateLastLoginAsync(user.Id);

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshTokenValue = _tokenService.GenerateRefreshToken();

        var jwtSettings = _configuration.GetSection("JwtSettings");
        var refreshTokenExpiryDays = int.Parse(jwtSettings["RefreshTokenExpiryDays"] ?? "7");
        var accessTokenExpiryMinutes = int.Parse(jwtSettings["AccessTokenExpiryMinutes"] ?? "60");

        // Revoke old refresh tokens
        await _refreshTokenRepository.RevokeAllByUserIdAsync(user.Id);

        // Save new refresh token
        var refreshToken = new Domain.Entities.RefreshToken
        {
            UserId = user.Id,
            Token = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiryDays),
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepository.CreateAsync(refreshToken);

        // Log successful login
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = user.Id,
            Action = "Login",
            Result = "Success",
            IpAddress = request.IpAddress,
            UserAgent = request.UserAgent,
            CreatedAt = DateTime.UtcNow
        });

        // Publish event to RabbitMQ
        try
        {
            await _messagePublisher.PublishAsync(new UserLoggedInEvent
            {
                UserId = user.Id,
                Email = user.Email,
                IpAddress = request.IpAddress,
                LoggedInAt = DateTime.UtcNow
            }, "user.logged-in");
        }
        catch (Exception)
        {
            // Log error but don't fail the request
        }

        // Update user's LastLoginAt for response
        user.LastLoginAt = DateTime.UtcNow;

        // Return response
        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddMinutes(accessTokenExpiryMinutes),
            User = _mapper.Map<UserDto>(user)
        };
    }

    private async Task LogFailedAttempt(Guid? userId, string email, string? ipAddress, string? userAgent, string reason)
    {
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = userId,
            Action = "Login",
            Result = "Failed",
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Details = $"{reason} - Email: {email}",
            CreatedAt = DateTime.UtcNow
        });
    }
}
