namespace ANews.Domain.Entities;

public class NewsSection : BaseEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public bool IsPublic { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public bool IsSystemSection { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    public string? IconClass { get; set; }
    public string? Color { get; set; } = "#4a90e2";
    public List<string> Keywords { get; set; } = [];
    public int? CreatedByUserId { get; set; }

    public ICollection<NewsEvent> Events { get; set; } = [];
    public ICollection<NewsSource> Sources { get; set; } = [];
}
