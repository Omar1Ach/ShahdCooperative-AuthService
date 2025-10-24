namespace ShahdCooperative.AuthService.Application.DTOs;

public class AuditLogDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public string Result { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
