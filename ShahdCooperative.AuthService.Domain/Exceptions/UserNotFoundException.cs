namespace ShahdCooperative.AuthService.Domain.Exceptions;

public class UserNotFoundException : AuthException
{
    public UserNotFoundException() : base("User not found.")
    {
    }

    public UserNotFoundException(string message) : base(message)
    {
    }
}
