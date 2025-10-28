# Docker Setup Guide

## Prerequisites
- Docker Desktop installed
- Docker Compose installed

## Running with Docker Compose

### Start all services (SQL Server, RabbitMQ, AuthService API)

```bash
docker-compose up -d
```

This will start:
- **SQL Server Developer 2022** on port `1433`
- **RabbitMQ** on ports `5672` (AMQP) and `15672` (Management UI)
- **AuthService API** on port `5000`

### View logs

```bash
docker-compose logs -f
```

### View logs for specific service

```bash
docker-compose logs -f authservice
```

### Stop all services

```bash
docker-compose down
```

### Stop and remove volumes (clean database)

```bash
docker-compose down -v
```

## Service URLs

- **AuthService API**: http://localhost:5000
- **AuthService Swagger**: http://localhost:5000/swagger
- **RabbitMQ Management**: http://localhost:15672 (admin/admin123)

## SQL Server Connection

**Connection String for external tools (SSMS, Azure Data Studio):**
```
Server=localhost,1433;Database=ShahdCooperative;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True
```

## Development Modes

### Option 1: Run locally without Docker
```bash
dotnet run --project ShahdCooperative.AuthService.API
```
Uses local SQL Server Express with Integrated Security

### Option 2: Run with Docker
```bash
docker-compose up
```
Uses containerized SQL Server Developer edition with SQL Server authentication

## Environment Variables

You can override any configuration in `docker-compose.yml`:

```yaml
environment:
  - ConnectionStrings__DefaultConnection=YourConnectionString
  - RabbitMQ__Host=your-rabbitmq-host
  - JwtSettings__SecretKey=YourSecretKey
  # etc...
```

## Building the Docker Image Manually

```bash
docker build -t shahdcooperative-authservice .
```

## Running Single Container

```bash
docker run -p 5000:8080 \
  -e ConnectionStrings__DefaultConnection="Server=host.docker.internal,1433;Database=ShahdCooperative;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True" \
  -e RabbitMQ__Host=host.docker.internal \
  -e JwtSettings__SecretKey=YourSuperSecretKeyForJWTThatIsAtLeast32CharactersLong! \
  shahdcooperative-authservice
```

## Database Migrations

Run migrations inside the container:

```bash
docker-compose exec authservice dotnet ef database update
```

Or run migrations from your local machine:

```bash
dotnet ef database update --project ShahdCooperative.AuthService.Infrastructure --startup-project ShahdCooperative.AuthService.API
```

## Configuration

### JWT Settings
Update JWT settings in `docker-compose.yml` or `appsettings.Production.json`:
- **SecretKey**: Must be at least 32 characters
- **AccessTokenExpiryMinutes**: Default 60 minutes
- **RefreshTokenExpiryDays**: Default 7 days

### RabbitMQ Settings
- **Exchange**: `shahdcooperative.events`
- **VirtualHost**: `/`
- **Username**: `admin`
- **Password**: `admin123`

### Rate Limiting
- **Auth endpoints**: 5 requests per 15 minutes
- **API endpoints**: 100 requests per minute
- **Admin endpoints**: 50 requests per 5 minutes

## Troubleshooting

### SQL Server container won't start
- Ensure you have enough memory allocated to Docker (minimum 2GB)
- Check if port 1433 is already in use
- Verify password meets SQL Server complexity requirements

### RabbitMQ connection fails
- Wait for RabbitMQ to fully start (check health: `docker-compose ps`)
- Ensure port 5672 is not in use
- Check RabbitMQ logs: `docker-compose logs rabbitmq`

### API can't connect to SQL Server
- Check SQL Server health: `docker-compose ps`
- Verify SQL Server is accepting connections
- Check connection string in `docker-compose.yml`
- Ensure database is created and migrations are applied

### Authentication issues
- Verify JWT secret key is configured correctly
- Check token expiry settings
- Review API logs: `docker-compose logs authservice`

## Security Notes

⚠️ **IMPORTANT**: The passwords and secrets in the docker-compose.yml are for development only.

**For production:**
1. Use environment variables or secrets management
2. Change all default passwords
3. Use strong JWT secret keys
4. Enable HTTPS/SSL
5. Configure proper firewall rules
6. Use secure RabbitMQ credentials
