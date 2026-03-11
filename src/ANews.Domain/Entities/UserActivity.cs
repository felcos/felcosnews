using ANews.Domain.Enums;

namespace ANews.Domain.Entities;

public class UserActivity
{
    public long Id { get; set; }
    public int UserId { get; set; }
    public ActivityType ActivityType { get; set; }
    public int? NewsSectionId { get; set; }
    public int? NewsEventId { get; set; }
    public int? NewsArticleId { get; set; }
    public int? StoryThreadId { get; set; }
    public string? Metadata { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
