using ANews.Domain.Enums;

namespace ANews.Domain.Entities;

public class NotificationChannel : BaseEntity
{
    public int UserId { get; set; }
    public required string Name { get; set; }
    public NotificationChannelType Type { get; set; }
    public required string Config { get; set; }
    public bool IsVerified { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public string? VerificationToken { get; set; }
    public DateTime? VerifiedAt { get; set; }
    public int TotalSent { get; set; } = 0;
    public int TotalFailed { get; set; } = 0;
    public DateTime? LastUsedAt { get; set; }

    public ICollection<NotificationLog> Logs { get; set; } = [];
}
