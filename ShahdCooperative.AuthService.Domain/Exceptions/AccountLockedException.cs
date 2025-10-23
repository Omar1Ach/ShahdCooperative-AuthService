namespace ShahdCooperative.AuthService.Domain.Exceptions;

public class AccountLockedException : AuthException
{
    public DateTime LockoutEnd { get; }

    public AccountLockedException(DateTime lockoutEnd)
        : base($"Account is locked until {lockoutEnd:yyyy-MM-dd HH:mm:ss} UTC.")
    {
        LockoutEnd = lockoutEnd;
    }
}
