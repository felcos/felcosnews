namespace ANews.Domain.Entities;

public class ReaderProfile : BaseEntity
{
    public int UserId { get; set; }
    public string? SemanticProfile { get; set; }
    public string[] TopInterests { get; set; } = [];
    public string[] AvoidTopics { get; set; } = [];
    public string? PreferredDepth { get; set; }
    public int ArticlesRead { get; set; } = 0;
    public int EventsOpened { get; set; } = 0;
    public DateTime? LastAnalyzedAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
}
