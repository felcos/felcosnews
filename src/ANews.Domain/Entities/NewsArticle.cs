using ANews.Domain.Enums;

namespace ANews.Domain.Entities;

public class NewsArticle : BaseEntity
{
    public required string Title { get; set; }
    public string? Content { get; set; }
    public string? Summary { get; set; }
    public required string SourceUrl { get; set; }
    public required string SourceName { get; set; }
    public DateTime PublishedAt { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public ArticleType ArticleType { get; set; } = ArticleType.Main;
    public decimal Relevance { get; set; } = 50;
    public decimal SentimentScore { get; set; } = 0;
    public decimal CredibilityScore { get; set; } = 70;
    public string Language { get; set; } = "es";
    public string? ImageUrl { get; set; }
    public string[] Keywords { get; set; } = [];
    public string? ContentHash { get; set; }
    public int NewsEventId { get; set; }
    public int? NewsSourceId { get; set; }

    public NewsEvent Event { get; set; } = null!;
    public NewsSource? Source { get; set; }
    public ICollection<Bookmark> Bookmarks { get; set; } = [];
}
