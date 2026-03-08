using ANews.Domain.Enums;

namespace ANews.Domain.Entities;

public class UserModule : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public int UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public bool NotificationsEnabled { get; set; } = true;
    public NotificationFrequency NotificationFrequency { get; set; } = NotificationFrequency.Instant;
    public string? IconClass { get; set; } = "fa-newspaper";
    public string? Color { get; set; } = "#4a90e2";
    public int SortOrder { get; set; } = 0;
    public string? RssFeedToken { get; set; }
    public int TotalMatchedEvents { get; set; } = 0;
    public DateTime? LastMatchAt { get; set; }

    public ICollection<ModuleKeyword> Keywords { get; set; } = [];
    public ICollection<NotificationLog> NotificationLogs { get; set; } = [];
}
