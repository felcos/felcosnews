namespace ANews.Domain.Entities;

public class MorningBrief : BaseEntity
{
    public DateTime BriefDate { get; set; } = DateTime.UtcNow.Date;
    public required string Headline { get; set; }
    public required string TopStories { get; set; }
    public string? DeepDive { get; set; }
    public string? Developing { get; set; }
    public string? Surprise { get; set; }
    public int TopStoriesCount { get; set; }
    public int TotalEventsAnalyzed { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
