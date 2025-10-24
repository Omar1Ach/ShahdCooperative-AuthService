using AutoMapper;
using MediatR;
using Microsoft.Extensions.Configuration;
using ShahdCooperative.AuthService.Application.DTOs;
using ShahdCooperative.AuthService.Domain.Entities;
using ShahdCooperative.AuthService.Domain.Enums;
using ShahdCooperative.AuthService.Domain.Events;
using ShahdCooperative.AuthService.Domain.Interfaces;
using System.Security.Cryptography;

namespace ShahdCooperative.AuthService.Application.Commands.Register;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, LoginResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IMapper _mapper;
    private readonly IConfiguration _configuration;
    private readonly IMessagePublisher _messagePublisher;
    private readonly IEmailService _emailService;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IAuditLogRepository auditLogRepository,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IMapper mapper,
        IConfiguration configuration,
        IMessagePublisher messagePublisher,
        IEmailService emailService)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _auditLogRepository = auditLogRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _mapper = mapper;
        _configuration = configuration;
        _messagePublisher = messagePublisher;
        _emailService = emailService;
    }

    public async Task<LoginResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        // Check if user already exists
        if (await _userRepository.ExistsAsync(request.Email))
        {
            throw new InvalidOperationException("User with this email already exists.");
        }

        // Hash password
        var passwordHash = _passwordHasher.HashPassword(request.Password, out var salt);

        // Generate email verification token
        var verificationToken = GenerateSecureToken();

        // Create user
        var user = new User
        {
            Email = request.Email.ToLowerInvariant(),
            PasswordHash = passwordHash,
            PasswordSalt = salt,
            Role = nameof(UserRole.Customer),
            IsActive = true,
            IsEmailVerified = false,
            EmailVerificationToken = verificationToken,
            EmailVerificationExpiry = DateTime.UtcNow.AddHours(24), // Token expires in 24 hours
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var userId = await _userRepository.CreateAsync(user);
        user.Id = userId;

        // Send verification email
        try
        {
            await _emailService.SendEmailVerificationAsync(user.Email, verificationToken);
        }
        catch (Exception)
        {
            // Log error but don't fail the request
        }

        // Generate tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshTokenValue = _tokenService.GenerateRefreshToken();

        var jwtSettings = _configuration.GetSection("JwtSettings");
        var refreshTokenExpiryDays = int.Parse(jwtSettings["RefreshTokenExpiryDays"] ?? "7");
        var accessTokenExpiryMinutes = int.Parse(jwtSettings["AccessTokenExpiryMinutes"] ?? "60");

        // Save refresh token
        var refreshToken = new Domain.Entities.RefreshToken
        {
            UserId = userId,
            Token = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddDays(refreshTokenExpiryDays),
            CreatedAt = DateTime.UtcNow
        };

        await _refreshTokenRepository.CreateAsync(refreshToken);

        // Log audit
        await _auditLogRepository.CreateAsync(new AuditLog
        {
            UserId = userId,
            Action = "Register",
            Result = "Success",
            CreatedAt = DateTime.UtcNow
        });

        // Publish event to RabbitMQ
        try
        {
            await _messagePublisher.PublishAsync(new UserRegisteredEvent
            {
                UserId = userId,
                Email = user.Email,
                Role = user.Role,
                RegisteredAt = DateTime.UtcNow
            }, "user.registered");
        }
        catch (Exception)
        {
            // Log error but don't fail the request
        }

        // Return response
        return new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenValue,
            ExpiresAt = DateTime.UtcNow.AddMinutes(accessTokenExpiryMinutes),
            User = _mapper.Map<UserDto>(user)
        };
    }

    private static string GenerateSecureToken()
    {
        var randomBytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
