namespace ShahdCooperative.AuthService.Domain.Events;

public class UserRegisteredEvent
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime RegisteredAt { get; set; }
}
