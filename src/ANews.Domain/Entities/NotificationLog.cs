namespace ANews.Domain.Entities;

public class NotificationLog : BaseEntity
{
    public int UserId { get; set; }
    public int? NotificationChannelId { get; set; }
    public int? UserModuleId { get; set; }
    public int? NewsEventId { get; set; }
    public required string Message { get; set; }
    public bool Sent { get; set; }
    public string? Error { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public NotificationChannel? Channel { get; set; }
    public UserModule? Module { get; set; }
    public NewsEvent? NewsEvent { get; set; }
}
