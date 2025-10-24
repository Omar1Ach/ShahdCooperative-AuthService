using AutoMapper;
using MediatR;
using Microsoft.Extensions.Configuration;
using ShahdCooperative.AuthService.Application.DTOs;
using ShahdCooperative.AuthService.Domain.Entities;
using ShahdCooperative.AuthService.Domain.Events;
using ShahdCooperative.AuthService.Domain.Interfaces;

namespace ShahdCooperative.AuthService.Application.Commands.ExternalLogin;

public class ExternalLoginCommandHandler : IRequestHandler<ExternalLoginCommand, LoginResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IExternalLoginRepository _externalLoginRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly ITokenService _tokenService;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;
    private readonly IMessagePublisher _messagePublisher;

    public ExternalLoginCommandHandler(
        IUserRepository userRepository,
        IExternalLoginRepository externalLoginRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IAuditLogRepository auditLogRepository,
        ITokenService tokenService,
        IMapper mapper,
        IConfiguration configuration,
        IMessagePublisher messagePublisher)
    {
        _userRepository = userRepository;
        _externalLoginRepository = externalLoginRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _auditLogRepository = auditLogRepository;
        _tokenService = tokenService;
        _mapper = mapper;
        _configuration = configuration;
        _messagePublisher = messagePublisher;
    }

    public async Task<LoginResponse> Handle(ExternalLoginCommand request, CancellationToken cancellationToken)
    {
        // Scenario 1: External login already exists → Login existing user
        var existingExternalLogin = await _externalLoginRepository.GetByProviderAndKeyAsync(request.Provider, request.ProviderKey);

        if (existingExternalLogin != null)
        {
            var user = await _userRepository.GetByIdAsync(existingExternalLogin.UserId);
            if (user == null)
            {
                throw new InvalidOperationException("User associated with external login not found");
            }

            // Check if account is active
            if (!user.IsActive)
            {
                throw new InvalidOperationException("Account is inactive");
            }

            // Update last login for external login
            await _externalLoginRepository.UpdateLastLoginAsync(existingExternalLogin.Id);

            // Update user last login
            await _userRepository.UpdateLastLoginAsync(user.Id);

            // Log successful login
            await LogAuditAsync(user.Id, "External Login", "Success", request.IpAddress, request.UserAgent,
                $"User {user.Email} logged in with {request.Provider}");

            return await GenerateLoginResponseAsync(user, request.IpAddress, request.UserAgent);
        }

        // Scenario 2: Check if user with this email already exists → Link external login
        var existingUser = await _userRepository.GetByEmailAsync(request.Email.ToLowerInvariant());

        if (existingUser != null)
        {
            // Check if account is active
            if (!existingUser.IsActive)
            {
                throw new InvalidOperationException("Account is inactive");
            }

            // Link external login to existing user
            var newExternalLogin = new Domain.Entities.ExternalLogin
            {
                UserId = existingUser.Id,
                Provider = request.Provider,
                ProviderKey = request.ProviderKey,
                ProviderDisplayName = request.ProviderDisplayName,
                Email = request.Email,
                LastLoginAt = DateTime.UtcNow
            };

            await _externalLoginRepository.CreateAsync(newExternalLogin);

            // Update user last login
            await _userRepository.UpdateLastLoginAsync(existingUser.Id);

            // Log successful link and login
            await LogAuditAsync(existingUser.Id, "External Login Linked", "Success", request.IpAddress, request.UserAgent,
                $"User {existingUser.Email} linked {request.Provider} account and logged in");

            return await GenerateLoginResponseAsync(existingUser, request.IpAddress, request.UserAgent);
        }

        // Scenario 3: New user → Create user with external login only (no password)
        var newUser = new User
        {
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = string.Empty,  // No password for external-only users
            PasswordSalt = string.Empty,
            Role = "Customer",
            IsActive = true,
            IsEmailVerified = true, // Trust provider's email verification
            HasPassword = false, // Mark as external login only
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var userId = await _userRepository.CreateAsync(newUser);

        // Create external login entry
        var externalLogin = new Domain.Entities.ExternalLogin
        {
            UserId = userId,
            Provider = request.Provider,
            ProviderKey = request.ProviderKey,
            ProviderDisplayName = request.ProviderDisplayName,
            Email = request.Email,
            LastLoginAt = DateTime.UtcNow
        };

        await _externalLoginRepository.CreateAsync(externalLogin);

        // Update user last login
        await _userRepository.UpdateLastLoginAsync(userId);

        // Log successful registration and login
        await LogAuditAsync(userId, "External Registration", "Success", request.IpAddress, request.UserAgent,
            $"New user registered with {request.Provider}: {request.Email}");

        // Publish event
        try
        {
            await _messagePublisher.PublishAsync(new UserRegisteredEvent
            {
                UserId = userId,
                Email = request.Email,
                Role = "Customer",
                RegisteredAt = DateTime.UtcNow
            }, "user.registered");
        }
        catch (Exception)
        {
            // Log error but don't fail the request
        }

        newUser.Id = userId;
        return await GenerateLoginResponseAsync(newUser, request.IpAddress, request.UserAgent);
    }

    private async Task<LoginResponse> GenerateLoginResponseAsync(User user, string? ipAddress, string? userAgent)
    {
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

        // Publish event
        try
        {
            await _messagePublisher.PublishAsync(new UserLoggedInEvent
            {
                UserId = user.Id,
                Email = user.Email,
                IpAddress = ipAddress,
                LoggedInAt = DateTime.UtcNow
            }, "user.logged-in");
        }
        catch (Exception)
        {
            // Log error but don't fail the request
        }

        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddMinutes(accessTokenExpiryMinutes),
            User = _mapper.Map<UserDto>(user),
            Requires2FA = false,
            Message = "Login successful"
        };
    }

    private async Task LogAuditAsync(Guid userId, string action, string result, string? ipAddress, string? userAgent, string details)
    {
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = userId,
            Action = action,
            Result = result,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Details = details,
            CreatedAt = DateTime.UtcNow
        });
    }
}
