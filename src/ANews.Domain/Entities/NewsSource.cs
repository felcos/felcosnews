using ANews.Domain.Enums;

namespace ANews.Domain.Entities;

public class NewsSource : BaseEntity
{
    public required string Name { get; set; }
    public required string Url { get; set; }
    public NewsSourceType Type { get; set; } = NewsSourceType.Rss;
    public int CredibilityScore { get; set; } = 70;
    public string Language { get; set; } = "es";
    public bool IsActive { get; set; } = true;
    public int NewsSectionId { get; set; }
    public DateTime? LastScannedAt { get; set; }
    public int TotalArticlesFound { get; set; } = 0;
    public int SuccessfulScans { get; set; } = 0;
    public int FailedScans { get; set; } = 0;
    public string? LastError { get; set; }
    public string? CustomHeaders { get; set; }

    public NewsSection Section { get; set; } = null!;
    public ICollection<NewsArticle> Articles { get; set; } = [];
}
