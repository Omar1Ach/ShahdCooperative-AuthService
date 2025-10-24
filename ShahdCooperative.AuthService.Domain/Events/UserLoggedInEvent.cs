namespace ShahdCooperative.AuthService.Domain.Events;

public class UserLoggedInEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public DateTime LoggedInAt { get; set; }
}
