namespace ShahdCooperative.AuthService.Domain.Exceptions;

public class InvalidCredentialsException : AuthException
{
    public InvalidCredentialsException() : base("Invalid email or password.")
    {
    }

    public InvalidCredentialsException(string message) : base(message)
    {
    }
}
