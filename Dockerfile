# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy solution and project files
COPY ["ShahdCooperative.AuthService.API/ShahdCooperative.AuthService.API.csproj", "ShahdCooperative.AuthService.API/"]
COPY ["ShahdCooperative.AuthService.Application/ShahdCooperative.AuthService.Application.csproj", "ShahdCooperative.AuthService.Application/"]
COPY ["ShahdCooperative.AuthService.Domain/ShahdCooperative.AuthService.Domain.csproj", "ShahdCooperative.AuthService.Domain/"]
COPY ["ShahdCooperative.AuthService.Infrastructure/ShahdCooperative.AuthService.Infrastructure.csproj", "ShahdCooperative.AuthService.Infrastructure/"]

# Restore dependencies
RUN dotnet restore "ShahdCooperative.AuthService.API/ShahdCooperative.AuthService.API.csproj"

# Copy all source files
COPY . .

# Build the application
WORKDIR "/src/ShahdCooperative.AuthService.API"
RUN dotnet build "ShahdCooperative.AuthService.API.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "ShahdCooperative.AuthService.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Copy published files
COPY --from=publish /app/publish .

# Set entry point
ENTRYPOINT ["dotnet", "ShahdCooperative.AuthService.API.dll"]
