using ANews.Domain.Enums;

namespace ANews.Domain.Entities;

public class NewsEvent : BaseEntity
{
    public required string Title { get; set; }
    public string? Description { get; set; }
    public EventPriority Priority { get; set; } = EventPriority.Medium;
    public decimal ImpactScore { get; set; } = 50;
    public string? Category { get; set; }
    public string? EventType { get; set; }
    public DateTime StartDate { get; set; } = DateTime.UtcNow;
    public DateTime? EndDate { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Location { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string[] Tags { get; set; } = [];
    public EventTrend Trend { get; set; } = EventTrend.Stable;
    public int NewsSectionId { get; set; }
    public int? ParentEventId { get; set; }
    public int? StoryThreadId { get; set; }
    public int CrossReferenceCount { get; set; } = 0;
    public int SourceDiversity { get; set; } = 0;

    public NewsSection Section { get; set; } = null!;
    public NewsEvent? ParentEvent { get; set; }
    public StoryThread? StoryThread { get; set; }
    public ICollection<NewsArticle> Articles { get; set; } = [];
    public ICollection<NewsEvent> RelatedEvents { get; set; } = [];
    public ICollection<AlertTrigger> AlertTriggers { get; set; } = [];
    public ICollection<EventBriefing> Briefings { get; set; } = [];
}
