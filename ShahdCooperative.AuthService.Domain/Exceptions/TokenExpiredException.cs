namespace ShahdCooperative.AuthService.Domain.Exceptions;

public class TokenExpiredException : AuthException
{
    public TokenExpiredException() : base("Token has expired.")
    {
    }

    public TokenExpiredException(string message) : base(message)
    {
    }
}
