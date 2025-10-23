using System.Net;
using System.Text.Json;
using FluentValidation;
using ShahdCooperative.AuthService.Domain.Exceptions;

namespace ShahdCooperative.AuthService.API.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        context.Response.ContentType = "application/json";

        object response = exception switch
        {
            ValidationException validationEx => new
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Message = "Validation failed",
                Errors = validationEx.Errors.Select(e => new
                {
                    Property = e.PropertyName,
                    Error = e.ErrorMessage
                })
            },
            InvalidCredentialsException => new
            {
                StatusCode = (int)HttpStatusCode.Unauthorized,
                Message = exception.Message
            },
            AccountLockedException accountLockedEx => new
            {
                StatusCode = (int)HttpStatusCode.Unauthorized,
                Message = exception.Message,
                LockoutEnd = accountLockedEx.LockoutEnd
            },
            UserNotFoundException => new
            {
                StatusCode = (int)HttpStatusCode.NotFound,
                Message = exception.Message
            },
            TokenExpiredException => new
            {
                StatusCode = (int)HttpStatusCode.Unauthorized,
                Message = exception.Message
            },
            InvalidOperationException => new
            {
                StatusCode = (int)HttpStatusCode.BadRequest,
                Message = exception.Message
            },
            _ => new
            {
                StatusCode = (int)HttpStatusCode.InternalServerError,
                Message = "An unexpected error occurred"
            }
        };

        var statusCode = exception switch
        {
            ValidationException => (int)HttpStatusCode.BadRequest,
            InvalidCredentialsException => (int)HttpStatusCode.Unauthorized,
            AccountLockedException => (int)HttpStatusCode.Unauthorized,
            UserNotFoundException => (int)HttpStatusCode.NotFound,
            TokenExpiredException => (int)HttpStatusCode.Unauthorized,
            InvalidOperationException => (int)HttpStatusCode.BadRequest,
            _ => (int)HttpStatusCode.InternalServerError
        };

        context.Response.StatusCode = statusCode;

        _logger.LogError(exception, "An error occurred: {Message}", exception.Message);

        var jsonResponse = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}
