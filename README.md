# 🔐 ShahdCooperative Auth Service

Production-grade JWT authentication microservice for the ShahdCooperative ecosystem.

## 🎯 Overview

This microservice handles user registration, login, JWT token generation, refresh tokens, password management, and user profile management for the ShahdCooperative platform.

## 🏗️ Architecture

- **.NET 8** - Latest LTS framework
- **Dapper** - High-performance data access
- **BCrypt** - Secure password hashing
- **MediatR** - CQRS pattern implementation
- **FluentValidation** - Input validation
- **Serilog** - Structured logging
- **SQL Server** - Database

## 📊 Database Schema

Uses existing `ShahdCooperative` database with `Security` schema:
- `Security.Users` - Main authentication table
- `Security.RefreshTokens` - Token refresh management
- `Security.AuditLogs` - Security audit trail
- `Security.UserProfiles` - User profile information

## 🔑 JWT Configuration

Generates tokens compatible with ShahdCooperative.API:
- **Issuer**: `ShahdCooperativeAuthService`
- **Audience**: `ShahdCooperativeAPI`
- **Access Token**: 60 minutes
- **Refresh Token**: 7 days

## 🚀 Features

### Phase 1: Core Authentication ✅
- User Registration
- User Login
- Token Refresh
- Logout

### Phase 2: Password Management 🚧
- Change Password
- Forgot Password
- Reset Password

### Phase 3: User Management 🚧
- Get Current User
- Update Profile
- Admin User Management

## 🛠️ Project Structure

```
ShahdCooperative.AuthService/
├── ShahdCooperative.AuthService.API/          # Web API Layer
├── ShahdCooperative.AuthService.Application/  # Business Logic (CQRS)
├── ShahdCooperative.AuthService.Domain/       # Domain Entities & Interfaces
└── ShahdCooperative.AuthService.Infrastructure/ # Data Access (Dapper)
```

## 🔒 Security Features

- BCrypt password hashing (work factor 12)
- Account lockout after 5 failed attempts
- Refresh token rotation
- Security audit logging
- Rate limiting
- JWT token validation

## 📝 API Endpoints

### Authentication
- `POST /api/auth/register` - Register new user
- `POST /api/auth/login` - Login and get tokens
- `POST /api/auth/refresh` - Refresh access token
- `POST /api/auth/logout` - Logout and revoke tokens

### Password Management
- `POST /api/auth/change-password` - Change password
- `POST /api/auth/forgot-password` - Request password reset
- `POST /api/auth/reset-password` - Reset password with token

### User Management
- `GET /api/auth/me` - Get current user info
- `PUT /api/auth/profile` - Update user profile

## 🚀 Getting Started

### Prerequisites
- .NET 8 SDK
- SQL Server 2022
- Git

### Setup

1. Clone the repository
```bash
git clone https://github.com/Omar1Ach/ShahdCooperative-AuthService.git
cd ShahdCooperative-AuthService
```

2. Update connection string in `appsettings.json`

3. Run database migrations (tables will be created automatically)

4. Run the application
```bash
dotnet run --project ShahdCooperative.AuthService.API
```

5. Access Swagger UI at `https://localhost:7000/swagger`

## 🧪 Testing

```bash
dotnet test
```

## 📦 Deployment

Docker support coming soon!

## 📄 License

Proprietary - ShahdCooperative

## 👥 Team

Developed for the ShahdCooperative platform
