namespace ShahdCooperative.AuthService.Domain.Events;

public class UserLoggedOutEvent
{
    public Guid UserId { get; set; }
    public DateTime LoggedOutAt { get; set; }
}
