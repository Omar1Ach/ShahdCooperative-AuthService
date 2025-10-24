# ShahdCooperative Authentication Service

**Production-grade JWT authentication microservice** for the ShahdCooperative ecosystem, implementing enterprise-level security features including OAuth2, Two-Factor Authentication, rate limiting, and comprehensive audit logging.

**Author:** Omar Achbani
**Framework:** .NET 9
**Architecture:** Clean Architecture with CQRS

---

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Features](#features)
- [Technology Stack](#technology-stack)
- [Project Structure](#project-structure)
- [API Endpoints](#api-endpoints)
- [Security Features](#security-features)
- [Database Schema](#database-schema)
- [Getting Started](#getting-started)
- [Configuration](#configuration)
- [Testing](#testing)
- [Deployment](#deployment)

---

## Overview

ShahdCooperative.AuthService is a comprehensive authentication and authorization microservice designed with enterprise security standards. It provides a complete solution for user authentication, including traditional email/password login, OAuth2 social login, two-factor authentication, and robust security features.

### Key Highlights

- **Multiple Authentication Methods**: Email/Password, Google OAuth, Facebook OAuth
- **Two-Factor Authentication**: TOTP-based with QR codes and backup codes
- **Security First**: Rate limiting, CAPTCHA verification, account lockout, audit logging
- **Clean Architecture**: Separation of concerns with 4-layer architecture
- **CQRS Pattern**: Command Query Responsibility Segregation with MediatR
- **Production Ready**: Comprehensive testing, logging, and error handling

---

## Architecture

This project follows **Clean Architecture** principles with clear separation of concerns:

```
┌─────────────────────────────────────────────────────────┐
│                     API Layer                            │
│  Controllers, Middleware, Program.cs                     │
│  (HTTP, Routing, Exception Handling)                     │
└───────────────────┬─────────────────────────────────────┘
                    │
┌─────────────────────────────────────────────────────────┐
│                  Application Layer                       │
│  Commands, Queries, DTOs, Validators                     │
│  (CQRS with MediatR, Business Orchestration)            │
└───────────────────┬─────────────────────────────────────┘
                    │
┌─────────────────────────────────────────────────────────┐
│                   Domain Layer                           │
│  Entities, Interfaces, Domain Events, Exceptions         │
│  (Business Rules, Domain Logic)                          │
└───────────────────┬─────────────────────────────────────┘
                    │
┌─────────────────────────────────────────────────────────┐
│                Infrastructure Layer                      │
│  Repositories, Services, External Integrations           │
│  (Data Access, Email, CAPTCHA, OAuth, RabbitMQ)         │
└─────────────────────────────────────────────────────────┘
```

### Design Patterns

- **CQRS**: Commands for write operations, Queries for read operations
- **Repository Pattern**: Abstraction over data access layer
- **Dependency Injection**: Loose coupling and testability
- **Domain Events**: Event-driven architecture with RabbitMQ
- **Mediator Pattern**: Decoupled request/response handling

### Layer Responsibilities

#### API Layer
- HTTP request handling and routing
- Global exception handling middleware
- Rate limiting middleware
- Authentication/Authorization setup
- Swagger/OpenAPI documentation

#### Application Layer
- Command and query handlers (17 commands, 3 queries)
- Input validation with FluentValidation
- DTOs for data transfer
- Business logic orchestration

#### Domain Layer
- Core entities (User, RefreshToken, AuditLog, ExternalLogin, etc.)
- Domain interfaces (repositories and services)
- Business rules and domain logic
- Custom exceptions
- Domain events

#### Infrastructure Layer
- Data access with Dapper ORM
- External service integrations (Email, CAPTCHA, OAuth)
- Password hashing (BCrypt)
- JWT token generation and validation
- TOTP/2FA implementation
- Message publishing (RabbitMQ)

---

## Features

### Authentication

#### Traditional Authentication
- **User Registration**
  - Email-based registration with validation
  - Secure password hashing (BCrypt, work factor 12)
  - Email verification token generation (24-hour expiry)
  - CAPTCHA verification on registration
  - Automatic audit logging

- **User Login**
  - Email and password authentication
  - Failed login attempt tracking (max 5 attempts)
  - Automatic account lockout after 5 failed attempts (15-minute duration)
  - CAPTCHA verification on login
  - JWT token generation (access + refresh tokens)
  - Two-factor authentication detection and flow initiation

- **Token Management**
  - JWT access tokens (60-minute expiry, configurable)
  - Refresh tokens (7-day expiry, configurable)
  - Token rotation on refresh
  - Token revocation on logout
  - Cryptographically secure token generation

#### OAuth2 Social Login
- **Google OAuth Integration**
  - OAuth 2.0 authorization code flow
  - Automatic user creation for new users
  - Account linking for existing email addresses
  - Session management and token generation

- **Facebook OAuth Integration**
  - OAuth 2.0 authorization code flow
  - Email permission request
  - Automatic user creation and account linking

- **External Login Management**
  - List all linked external logins per user
  - Unlink OAuth providers
  - Support for password-less accounts (OAuth-only users)

#### Two-Factor Authentication (2FA)
- **TOTP Implementation**
  - Time-based One-Time Password (RFC 6238)
  - 30-second time window with ±1 step tolerance
  - Base32-encoded secret key generation (20 bytes/160 bits)
  - QR code generation for authenticator apps
  - Compatible with Google Authenticator, Microsoft Authenticator, Authy

- **Backup Codes**
  - 10 single-use backup codes per user
  - 8-character alphanumeric codes
  - Secure hashing for storage
  - Automatic removal after use

- **2FA Flow**
  1. Enable 2FA endpoint returns secret, QR code, and backup codes
  2. User scans QR code with authenticator app
  3. Verify setup with TOTP code
  4. Login requires 2FA code after password verification
  5. Option to disable 2FA with password confirmation

### Password Management

- **Change Password**
  - Requires current password verification
  - Strong password validation
  - Email notification sent after change
  - Token invalidation (logout all sessions)

- **Forgot Password**
  - Secure token generation (32-byte random, 1-hour expiry)
  - Email with reset link
  - Email enumeration prevention (always returns success)
  - Token usage tracking (single-use)

- **Reset Password**
  - Token validation and expiry check
  - Password strength requirements
  - Token invalidation after use
  - Confirmation email notification

### Email Management

- **Email Verification**
  - Verification token generation on registration
  - 24-hour token expiry
  - Resend verification email support
  - Account status tracking

- **Email Notifications**
  - Registration verification email
  - Password reset email with secure link
  - Password change confirmation
  - Two-factor authentication status changes
  - Template-ready for SMTP/SendGrid/AWS SES integration

### User Management

- **Profile Management**
  - Extended user profile information (name, phone, address, DOB, profile picture)
  - Profile update endpoints
  - User data retrieval

- **Account Status**
  - Active/Inactive status
  - Email verification status
  - Account lockout status with end timestamp
  - Soft delete support

### Admin Features

- **User Administration**
  - Paginated user listing with search and sorting
  - Lock/unlock user accounts manually
  - Delete users (soft delete, preserves audit trail)
  - Update user roles (Customer, Admin)
  - View detailed user information

- **Security Dashboard**
  - Total users count
  - Active users count
  - Locked accounts count
  - Unverified emails count
  - Daily login statistics (success/failed)
  - Recent activity feed (10 latest actions)

- **Audit Log Management**
  - View comprehensive audit logs with pagination
  - Filter by user ID, action type, date range, result
  - Track all security-relevant actions:
    - Login attempts (success/failure with reasons)
    - Registration events
    - Password changes and resets
    - Email verifications
    - 2FA enable/disable/verify
    - OAuth login/linking
    - Admin actions (lock, unlock, delete, role changes)

### Security Features

#### Rate Limiting
- **Policy-Based Limits**
  - `auth` policy: 5 requests per 15 minutes (sensitive operations)
  - `api` policy: 100 requests per 1 minute (general API)
  - `admin` policy: 50 requests per 5 minutes (admin operations)
- IP-based tracking with X-Forwarded-For support
- Custom `[RateLimit]` attribute for easy endpoint protection
- Returns HTTP 429 (Too Many Requests) with retry-after header

#### CAPTCHA Verification
- **Google reCAPTCHA v3 Integration**
  - Score-based validation (0.0-1.0 scale)
  - Configurable minimum score threshold (default: 0.5)
  - Applied to sensitive endpoints (register, login, OAuth, 2FA)
  - IP address tracking
  - Can be disabled for testing environments

#### Account Lockout
- Automatic lockout after 5 failed login attempts
- 15-minute lockout duration (configurable)
- Admin can manually lock/unlock accounts
- Lockout end timestamp tracking
- Automatic unlock after duration expires

#### Audit Logging
- Comprehensive tracking of all security events
- Records: User ID, action type, result, IP address, user agent, timestamp
- Detailed descriptions for troubleshooting
- Admin dashboard with activity feed
- Query and filtering capabilities

#### Token Security
- Cryptographically secure random token generation (`RandomNumberGenerator`)
- Token types:
  - Refresh tokens: 64-byte random (Base64)
  - Email verification: 32-byte random (Base64)
  - Password reset: 32-byte random (Base64)
  - 2FA backup codes: 8-character alphanumeric
- Single-use enforcement for sensitive tokens
- Proper expiry times for all token types

#### JWT Configuration
- **Claims**: Subject (User ID), Email, Role, JWT ID, Issued At
- **Validation**: Issuer, audience, lifetime, signature (HMAC-SHA256)
- Zero clock skew for strict expiry
- Configurable token lifetimes

---

## Technology Stack

### Core Technologies
- **.NET 9** - Latest .NET framework
- **C# 13** - Modern C# features
- **ASP.NET Core** - Web API framework

### Data Access & Storage
- **SQL Server** - Primary database
- **Dapper** - Lightweight ORM for high performance
- **Microsoft.Data.SqlClient** - SQL Server driver

### Architecture & Patterns
- **MediatR** - CQRS implementation
- **FluentValidation** - Request validation
- **AutoMapper** - Object-to-object mapping

### Security
- **BCrypt.Net-Next** - Password hashing
- **Microsoft.AspNetCore.Authentication.JwtBearer** - JWT authentication
- **Microsoft.AspNetCore.Authentication.Google** - Google OAuth
- **Microsoft.AspNetCore.Authentication.Facebook** - Facebook OAuth
- **OtpNet** - TOTP implementation (RFC 6238)
- **QRCoder** - QR code generation

### External Integrations
- **Google reCAPTCHA v3** - Bot protection
- **RabbitMQ.Client** - Message queue for domain events
- **Serilog** - Structured logging

### Testing
- **xUnit** - Testing framework
- **Moq** - Mocking framework
- **FluentAssertions** - Fluent test assertions

---

## Project Structure

```
ShahdCooperative.AuthService/
│
├── ShahdCooperative.AuthService.API/
│   ├── Controllers/
│   │   ├── AuthController.cs                  # Core auth endpoints
│   │   ├── TwoFactorController.cs             # 2FA management
│   │   ├── ExternalAuthController.cs          # OAuth endpoints
│   │   └── AdminController.cs                 # Admin operations
│   ├── Middleware/
│   │   ├── ExceptionHandlingMiddleware.cs     # Global exception handler
│   │   └── RateLimitingMiddleware.cs          # Rate limiting logic
│   ├── Program.cs                              # Application startup
│   └── appsettings.json                        # Configuration
│
├── ShahdCooperative.AuthService.Application/
│   ├── Commands/                               # Write operations (17 handlers)
│   │   ├── Login/
│   │   ├── Register/
│   │   ├── Logout/
│   │   ├── RefreshToken/
│   │   ├── ChangePassword/
│   │   ├── ForgotPassword/
│   │   ├── ResetPassword/
│   │   ├── VerifyEmail/
│   │   ├── Enable2FA/
│   │   ├── Verify2FASetup/
│   │   ├── Verify2FACode/
│   │   ├── Disable2FA/
│   │   ├── ExternalLogin/
│   │   ├── DeleteUser/
│   │   ├── LockUser/
│   │   ├── UnlockUser/
│   │   └── UpdateUserRole/
│   ├── Queries/                                # Read operations (3 handlers)
│   │   ├── GetAllUsers/
│   │   ├── GetAuditLogs/
│   │   └── GetSecurityDashboard/
│   ├── DTOs/                                   # Data Transfer Objects
│   └── DependencyInjection.cs
│
├── ShahdCooperative.AuthService.Domain/
│   ├── Entities/
│   │   ├── User.cs                             # Core user entity
│   │   ├── RefreshToken.cs                     # Token management
│   │   ├── AuditLog.cs                         # Security audit trail
│   │   ├── UserProfile.cs                      # Extended user info
│   │   ├── PasswordResetToken.cs               # Reset token tracking
│   │   └── ExternalLogin.cs                    # OAuth provider linkage
│   ├── Interfaces/
│   │   ├── IUserRepository.cs
│   │   ├── IRefreshTokenRepository.cs
│   │   ├── IAuditLogRepository.cs
│   │   ├── IPasswordResetTokenRepository.cs
│   │   ├── IExternalLoginRepository.cs
│   │   ├── IPasswordHasher.cs
│   │   ├── ITokenService.cs
│   │   ├── IEmailService.cs
│   │   ├── ICaptchaService.cs
│   │   ├── ITwoFactorService.cs
│   │   └── IMessagePublisher.cs
│   ├── Enums/
│   │   └── UserRole.cs                         # Customer, Admin
│   ├── Events/
│   │   ├── UserRegisteredEvent.cs
│   │   ├── UserLoggedInEvent.cs
│   │   └── UserLoggedOutEvent.cs
│   └── Exceptions/
│       ├── AuthException.cs
│       ├── InvalidCredentialsException.cs
│       ├── AccountLockedException.cs
│       ├── UserNotFoundException.cs
│       └── TokenExpiredException.cs
│
├── ShahdCooperative.AuthService.Infrastructure/
│   ├── Repositories/                           # Data access layer
│   │   ├── UserRepository.cs
│   │   ├── RefreshTokenRepository.cs
│   │   ├── AuditLogRepository.cs
│   │   ├── PasswordResetTokenRepository.cs
│   │   └── ExternalLoginRepository.cs
│   ├── Services/                               # External integrations
│   │   ├── PasswordHasher.cs                   # BCrypt implementation
│   │   ├── TokenService.cs                     # JWT generation/validation
│   │   ├── TwoFactorService.cs                 # TOTP and backup codes
│   │   ├── EmailService.cs                     # Email sending
│   │   ├── GoogleRecaptchaService.cs           # CAPTCHA verification
│   │   └── RabbitMQPublisher.cs                # Event publishing
│   └── DependencyInjection.cs
│
└── Tests/
    ├── ShahdCooperative.AuthService.API.Tests/
    ├── ShahdCooperative.AuthService.Application.Tests/
    └── ShahdCooperative.AuthService.Infrastructure.Tests/
```

---

## API Endpoints

### Authentication Endpoints

#### User Registration & Login
```http
POST /api/auth/register
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "captchaToken": "03AGdBq27..."
}

Response: 201 Created
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "xyz789...",
  "user": { "id": "guid", "email": "user@example.com", "role": "Customer" },
  "requires2FA": false
}
```

```http
POST /api/auth/login
Content-Type: application/json

{
  "email": "user@example.com",
  "password": "SecurePassword123!",
  "captchaToken": "03AGdBq27..."
}

Response: 200 OK (if no 2FA)
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "xyz789...",
  "user": { ... },
  "requires2FA": false
}

Response: 200 OK (if 2FA enabled)
{
  "requires2FA": true,
  "message": "2FA required"
}
```

#### Token Management
```http
POST /api/auth/refresh
Content-Type: application/json

{
  "refreshToken": "xyz789..."
}

Response: 200 OK
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "abc123..."
}
```

```http
POST /api/auth/logout
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "refreshToken": "xyz789..."
}

Response: 200 OK
```

### Two-Factor Authentication Endpoints

```http
POST /api/twofactor/enable
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "userId": "guid"
}

Response: 200 OK
{
  "secret": "JBSWY3DPEHPK3PXP",
  "qrCode": "data:image/png;base64,iVBORw0KGgoAAAANSUhEUg...",
  "backupCodes": ["ABCD1234", "EFGH5678", ...]
}
```

```http
POST /api/twofactor/verify-setup
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "userId": "guid",
  "code": "123456"
}

Response: 200 OK
```

```http
POST /api/twofactor/verify
Content-Type: application/json

{
  "email": "user@example.com",
  "code": "123456",
  "isBackupCode": false
}

Response: 200 OK
{
  "accessToken": "...",
  "refreshToken": "...",
  "user": { ... }
}
```

```http
POST /api/twofactor/disable
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "userId": "guid",
  "password": "SecurePassword123!"
}

Response: 200 OK
```

### OAuth2 External Login Endpoints

```http
GET /api/auth/external/google
Response: 302 Redirect to Google OAuth consent screen
```

```http
GET /api/auth/external/google/callback
(Handled by OAuth middleware, redirects to frontend with tokens)
```

```http
GET /api/auth/external/facebook
Response: 302 Redirect to Facebook OAuth consent screen
```

```http
GET /api/auth/external/facebook/callback
(Handled by OAuth middleware, redirects to frontend with tokens)
```

```http
GET /api/auth/external/list
Authorization: Bearer {accessToken}

Response: 200 OK
[
  {
    "provider": "Google",
    "providerDisplayName": "John Doe",
    "email": "john@gmail.com",
    "lastLoginAt": "2025-10-25T10:30:00Z",
    "createdAt": "2025-10-20T15:00:00Z"
  }
]
```

```http
DELETE /api/auth/external/unlink/{provider}
Authorization: Bearer {accessToken}

Response: 200 OK
{
  "message": "Successfully unlinked Google account"
}
```

### Password Management Endpoints

```http
POST /api/auth/change-password
Authorization: Bearer {accessToken}
Content-Type: application/json

{
  "userId": "guid",
  "currentPassword": "OldPassword123!",
  "newPassword": "NewSecurePassword456!"
}

Response: 200 OK
```

```http
POST /api/auth/forgot-password
Content-Type: application/json

{
  "email": "user@example.com"
}

Response: 200 OK (always, to prevent email enumeration)
{
  "message": "If account exists, reset email sent"
}
```

```http
POST /api/auth/reset-password
Content-Type: application/json

{
  "token": "base64-encoded-token",
  "newPassword": "NewSecurePassword789!"
}

Response: 200 OK
```

### Email Verification Endpoints

```http
POST /api/auth/verify-email
Content-Type: application/json

{
  "token": "base64-encoded-token"
}

Response: 200 OK
```

### Admin Endpoints (Requires Admin Role)

```http
GET /api/admin/users?pageNumber=1&pageSize=20&search=john&sortBy=email&sortOrder=asc
Authorization: Bearer {adminAccessToken}

Response: 200 OK
{
  "items": [ ... ],
  "totalCount": 100,
  "pageNumber": 1,
  "pageSize": 20,
  "totalPages": 5
}
```

```http
POST /api/admin/users/{userId}/lock
Authorization: Bearer {adminAccessToken}

Response: 200 OK
```

```http
POST /api/admin/users/{userId}/unlock
Authorization: Bearer {adminAccessToken}

Response: 200 OK
```

```http
DELETE /api/admin/users/{userId}
Authorization: Bearer {adminAccessToken}

Response: 204 No Content
```

```http
PUT /api/admin/users/{userId}/role
Authorization: Bearer {adminAccessToken}
Content-Type: application/json

{
  "role": "Admin"
}

Response: 200 OK
```

```http
GET /api/admin/audit-logs?pageNumber=1&pageSize=50&userId=guid&action=Login
Authorization: Bearer {adminAccessToken}

Response: 200 OK
{
  "items": [ ... ],
  "totalCount": 500,
  "pageNumber": 1,
  "pageSize": 50,
  "totalPages": 10
}
```

```http
GET /api/admin/dashboard
Authorization: Bearer {adminAccessToken}

Response: 200 OK
{
  "totalUsers": 1250,
  "activeUsers": 1180,
  "lockedUsers": 15,
  "unverifiedEmails": 55,
  "todaySuccessfulLogins": 342,
  "todayFailedLogins": 23,
  "recentActivities": [ ... ]
}
```

---

## Security Features

### Password Security
- **Hashing Algorithm**: BCrypt with work factor 12
- **Salt**: Automatically generated per password (64 bits)
- **No Plaintext Storage**: Passwords never stored in plain text
- **Verification Only**: BCrypt comparison without password retrieval

### Token Security
- **Access Tokens**:
  - JWT with HMAC-SHA256 signature
  - 60-minute expiry (configurable)
  - Contains: User ID, Email, Role, JWT ID, Issued At

- **Refresh Tokens**:
  - Cryptographically random (64 bytes)
  - 7-day expiry (configurable)
  - Rotation on use (new token replaces old)
  - Revocation support

- **Single-Use Tokens**:
  - Email verification tokens (24-hour expiry)
  - Password reset tokens (1-hour expiry)
  - Token usage tracked to prevent replay attacks

### Two-Factor Security
- **TOTP Standard**: RFC 6238 compliant
- **Secret Key**: 160-bit cryptographically random
- **QR Code**: Base64-encoded PNG for easy setup
- **Backup Codes**: Hashed storage, single-use, 10 codes
- **Time Window**: 30-second step with ±1 tolerance

### Account Protection
- **Failed Login Throttling**: Max 5 attempts before lockout
- **Account Lockout**: 15-minute automatic lockout
- **Manual Lock**: Admin can lock/unlock accounts
- **Soft Delete**: User data preserved for audit trail

### API Protection
- **Rate Limiting**: Per-IP request throttling
- **CAPTCHA**: Bot protection on sensitive endpoints
- **Audit Logging**: All security events logged
- **IP Tracking**: User agent and IP address recording
- **HTTPS Only**: Production configuration enforces HTTPS

### Data Protection
- **SQL Injection**: Parameterized queries with Dapper
- **XSS Protection**: Input validation and output encoding
- **CSRF**: Token validation on state-changing operations
- **Secrets Management**: Environment variables for sensitive config
- **CORS**: Configurable allowed origins

---

## Database Schema

### Security.Users
```sql
CREATE TABLE Security.Users (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Email NVARCHAR(255) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(255) NOT NULL,
    PasswordSalt NVARCHAR(255) NOT NULL,
    HasPassword BIT NOT NULL DEFAULT 1,
    Role NVARCHAR(50) NOT NULL DEFAULT 'Customer',
    IsActive BIT NOT NULL DEFAULT 1,
    IsEmailVerified BIT NOT NULL DEFAULT 0,
    EmailVerificationToken NVARCHAR(255),
    EmailVerificationExpiry DATETIME2,
    PasswordResetToken NVARCHAR(255),
    PasswordResetExpiry DATETIME2,
    FailedLoginAttempts INT NOT NULL DEFAULT 0,
    LockoutEnd DATETIME2,
    LastLoginAt DATETIME2,
    TwoFactorEnabled BIT NOT NULL DEFAULT 0,
    TwoFactorSecret NVARCHAR(255),
    BackupCodes NVARCHAR(MAX), -- JSON array
    IsDeleted BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
```

### Security.RefreshTokens
```sql
CREATE TABLE Security.RefreshTokens (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Token NVARCHAR(255) NOT NULL UNIQUE,
    ExpiresAt DATETIME2 NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    RevokedAt DATETIME2,
    ReplacedByToken NVARCHAR(255),
    FOREIGN KEY (UserId) REFERENCES Security.Users(Id)
);
```

### Security.AuditLogs
```sql
CREATE TABLE Security.AuditLogs (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER,
    Action NVARCHAR(100) NOT NULL,
    Result NVARCHAR(50) NOT NULL,
    IpAddress NVARCHAR(50),
    UserAgent NVARCHAR(500),
    Details NVARCHAR(MAX),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (UserId) REFERENCES Security.Users(Id)
);
```

### Security.ExternalLogins
```sql
CREATE TABLE Security.ExternalLogins (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Provider NVARCHAR(50) NOT NULL,
    ProviderKey NVARCHAR(255) NOT NULL,
    ProviderDisplayName NVARCHAR(255),
    Email NVARCHAR(255) NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    LastLoginAt DATETIME2,
    FOREIGN KEY (UserId) REFERENCES Security.Users(Id),
    UNIQUE (Provider, ProviderKey)
);
```

### Security.PasswordResetTokens
```sql
CREATE TABLE Security.PasswordResetTokens (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL,
    Token NVARCHAR(255) NOT NULL UNIQUE,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ExpiresAt DATETIME2 NOT NULL,
    UsedAt DATETIME2,
    FOREIGN KEY (UserId) REFERENCES Security.Users(Id)
);
```

### Security.UserProfiles
```sql
CREATE TABLE Security.UserProfiles (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    UserId UNIQUEIDENTIFIER NOT NULL UNIQUE,
    FirstName NVARCHAR(100),
    LastName NVARCHAR(100),
    PhoneNumber NVARCHAR(20),
    Address NVARCHAR(255),
    City NVARCHAR(100),
    Country NVARCHAR(100),
    DateOfBirth DATE,
    ProfilePictureUrl NVARCHAR(500),
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    FOREIGN KEY (UserId) REFERENCES Security.Users(Id)
);
```

---

## Getting Started

### Prerequisites

- **.NET 9 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **SQL Server 2022** or newer (Express edition works)
- **Visual Studio 2022** / **VS Code** / **Rider**
- **Git** for version control
- **Postman** or **Swagger** for API testing

### Installation

1. **Clone the repository**
```bash
git clone https://github.com/Omar1Ach/ShahdCooperative-AuthService.git
cd ShahdCooperative-AuthService
```

2. **Setup Database**
```sql
-- Create database
CREATE DATABASE ShahdCooperative;
GO

-- Run the schema creation scripts for all tables
-- (Scripts located in /Database/Schema/ folder if available)
```

3. **Configure Application**

Update `appsettings.json` or use environment variables:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=ShahdCooperative;Trusted_Connection=True;TrustServerCertificate=True;"
  },
  "JwtSettings": {
    "SecretKey": "your-super-secret-key-at-least-32-characters-long",
    "Issuer": "ShahdCooperativeAuthService",
    "Audience": "ShahdCooperativeAPI",
    "AccessTokenExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7
  },
  "OAuth": {
    "Google": {
      "ClientId": "your-google-client-id.apps.googleusercontent.com",
      "ClientSecret": "your-google-client-secret"
    },
    "Facebook": {
      "AppId": "your-facebook-app-id",
      "AppSecret": "your-facebook-app-secret"
    }
  },
  "GoogleRecaptcha": {
    "SiteKey": "your-recaptcha-site-key",
    "SecretKey": "your-recaptcha-secret-key",
    "Enabled": true,
    "MinimumScore": 0.5
  },
  "RabbitMQ": {
    "Host": "localhost",
    "Port": 5672,
    "Username": "guest",
    "Password": "guest",
    "VirtualHost": "/"
  }
}
```

4. **Restore Dependencies**
```bash
dotnet restore
```

5. **Build the Project**
```bash
dotnet build
```

6. **Run the Application**
```bash
dotnet run --project ShahdCooperative.AuthService.API
```

7. **Access Swagger UI**
```
https://localhost:7000/swagger
```

### OAuth Setup (Optional)

#### Google OAuth
1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project
3. Enable Google+ API
4. Create OAuth 2.0 credentials
5. Add authorized redirect URIs: `https://localhost:7000/api/auth/external/google/callback`
6. Copy Client ID and Client Secret to `appsettings.json`

#### Facebook OAuth
1. Go to [Facebook Developers](https://developers.facebook.com/)
2. Create a new app
3. Add Facebook Login product
4. Configure OAuth redirect URIs: `https://localhost:7000/api/auth/external/facebook/callback`
5. Copy App ID and App Secret to `appsettings.json`

### RabbitMQ Setup (Optional)

```bash
# Using Docker
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management

# Access management UI at http://localhost:15672
# Default credentials: guest/guest
```

---

## Configuration

### JWT Settings
```json
{
  "JwtSettings": {
    "SecretKey": "minimum-32-characters-secret-key",
    "Issuer": "ShahdCooperativeAuthService",
    "Audience": "ShahdCooperativeAPI",
    "AccessTokenExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7
  }
}
```

### Rate Limiting Policies
```json
{
  "RateLimiting": {
    "Auth": {
      "Limit": 5,
      "WindowMinutes": 15
    },
    "Api": {
      "Limit": 100,
      "WindowMinutes": 1
    },
    "Admin": {
      "Limit": 50,
      "WindowMinutes": 5
    }
  }
}
```

### CAPTCHA Settings
```json
{
  "GoogleRecaptcha": {
    "SiteKey": "6Lc...",
    "SecretKey": "6Lc...",
    "Enabled": true,
    "MinimumScore": 0.5
  }
}
```

### Email Settings (Template)
```json
{
  "EmailSettings": {
    "FromEmail": "noreply@shahdcooperative.com",
    "FromName": "ShahdCooperative",
    "SmtpHost": "smtp.gmail.com",
    "SmtpPort": 587,
    "SmtpUsername": "your-email@gmail.com",
    "SmtpPassword": "your-app-password",
    "UseSsl": true
  }
}
```

### Logging Configuration
```json
{
  "Serilog": {
    "Using": ["Serilog.Sinks.Console", "Serilog.Sinks.File"],
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    },
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "Logs/log-.txt",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

---

## Testing

### Run All Tests
```bash
dotnet test
```

### Run Specific Test Project
```bash
dotnet test ShahdCooperative.AuthService.Application.Tests
```

### Run with Coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

### Test Structure
- **Unit Tests**: Test individual components in isolation
- **Integration Tests**: Test component interactions
- **Command Handler Tests**: Verify business logic
- **Service Tests**: Test external integrations
- **Middleware Tests**: Validate request pipeline

### Example Test Cases
- User registration with valid data
- Login with correct credentials
- Login with incorrect password (5 attempts + lockout)
- 2FA setup and verification flow
- Password reset token generation and validation
- OAuth account linking scenarios
- Rate limiting enforcement
- JWT token generation and validation
- TOTP code verification with time tolerance

---

## Deployment

### Environment Variables

For production, use environment variables instead of `appsettings.json`:

```bash
export ConnectionStrings__DefaultConnection="Server=prod-server;..."
export JwtSettings__SecretKey="production-secret-key"
export OAuth__Google__ClientSecret="google-secret"
export OAuth__Facebook__AppSecret="facebook-secret"
export GoogleRecaptcha__SecretKey="recaptcha-secret"
export RabbitMQ__Password="rabbitmq-password"
```

### Docker Deployment

**Dockerfile** (create in project root):
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY ["ShahdCooperative.AuthService.API/ShahdCooperative.AuthService.API.csproj", "ShahdCooperative.AuthService.API/"]
COPY ["ShahdCooperative.AuthService.Application/ShahdCooperative.AuthService.Application.csproj", "ShahdCooperative.AuthService.Application/"]
COPY ["ShahdCooperative.AuthService.Domain/ShahdCooperative.AuthService.Domain.csproj", "ShahdCooperative.AuthService.Domain/"]
COPY ["ShahdCooperative.AuthService.Infrastructure/ShahdCooperative.AuthService.Infrastructure.csproj", "ShahdCooperative.AuthService.Infrastructure/"]
RUN dotnet restore "ShahdCooperative.AuthService.API/ShahdCooperative.AuthService.API.csproj"
COPY . .
WORKDIR "/src/ShahdCooperative.AuthService.API"
RUN dotnet build "ShahdCooperative.AuthService.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ShahdCooperative.AuthService.API.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ShahdCooperative.AuthService.API.dll"]
```

**Build and Run**:
```bash
docker build -t shahdcooperative-authservice:latest .
docker run -d -p 8080:80 -p 8443:443 \
  -e ConnectionStrings__DefaultConnection="..." \
  -e JwtSettings__SecretKey="..." \
  shahdcooperative-authservice:latest
```

### Production Checklist
- [ ] Update `JwtSettings:SecretKey` to strong random value
- [ ] Configure OAuth client secrets
- [ ] Setup SMTP/SendGrid for email service
- [ ] Configure RabbitMQ connection
- [ ] Enable HTTPS with valid SSL certificate
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Configure CORS allowed origins
- [ ] Setup database backups
- [ ] Configure logging and monitoring
- [ ] Review and adjust rate limiting policies
- [ ] Enable audit log retention policy

---

## Performance Considerations

- **Dapper ORM**: High-performance data access (faster than EF Core)
- **Connection Pooling**: SQL Server connection pooling enabled
- **Caching**: Rate limiting uses in-memory cache
- **Async/Await**: All I/O operations are asynchronous
- **Pagination**: Large result sets paginated to reduce memory usage
- **Token Validation**: JWT validation is stateless (no database lookup)
- **Password Hashing**: BCrypt work factor 12 (balance security/performance)

---

## Future Enhancements

- [ ] Email template engine (Razor/Handlebars)
- [ ] Additional OAuth providers (Microsoft, Apple, LinkedIn)
- [ ] WebAuthn/FIDO2 support for passwordless auth
- [ ] Session management dashboard for users
- [ ] Geolocation-based suspicious login detection
- [ ] Account activity timeline
- [ ] Export user data (GDPR compliance)
- [ ] Advanced password policies (complexity, history, expiry)
- [ ] Multi-tenancy support
- [ ] GraphQL API endpoint
- [ ] Real-time notifications (SignalR)
- [ ] Biometric authentication support

---

## Documentation

- **Swagger UI**: Available at `/swagger` when running in development
- **API Reference**: Auto-generated from XML comments
- **Architecture Diagrams**: See `/docs/architecture/` (if available)
- **Database ERD**: See `/docs/database/` (if available)

---

## License

**Proprietary License** - Copyright © 2025 ShahdCooperative
All rights reserved. Unauthorized copying, modification, distribution, or use of this software is strictly prohibited.

---

## Author

**Omar Achbani**
Full-Stack Developer

- GitHub: [@Omar1Ach](https://github.com/Omar1Ach)
- Project: [ShahdCooperative Authentication Service](https://github.com/Omar1Ach/ShahdCooperative-AuthService)

---

## Acknowledgments

This project leverages industry-standard security practices and modern .NET development patterns to provide a robust authentication solution for the ShahdCooperative ecosystem.

---

**Built for ShahdCooperative**
