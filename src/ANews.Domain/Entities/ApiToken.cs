namespace ANews.Domain.Entities;

public class ApiToken : BaseEntity
{
    public int UserId { get; set; }
    public required string Name { get; set; }
    public required string TokenHash { get; set; }
    public required string TokenPrefix { get; set; }
    public string[] Scopes { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public DateTime? ExpiresAt { get; set; }
    public DateTime? LastUsedAt { get; set; }
    public int UsageCount { get; set; } = 0;
}
