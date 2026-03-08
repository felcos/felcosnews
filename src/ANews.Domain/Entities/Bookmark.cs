namespace ANews.Domain.Entities;

public class Bookmark : BaseEntity
{
    public int UserId { get; set; }
    public int NewsArticleId { get; set; }
    public string? Note { get; set; }

    public NewsArticle Article { get; set; } = null!;
}
