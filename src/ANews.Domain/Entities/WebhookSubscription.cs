namespace ANews.Domain.Entities;

public class WebhookSubscription : BaseEntity
{
    public int UserId { get; set; }
    public required string Url { get; set; }
    public string? Secret { get; set; } // HMAC-SHA256 signing secret
    public string EventTypes { get; set; } = "high_priority"; // comma-separated: high_priority,breaking,all
    public bool IsActive { get; set; } = true;
    public int FailCount { get; set; } = 0;
    public DateTime? LastTriggeredAt { get; set; }
    public DateTime? LastFailedAt { get; set; }
    public string? LastError { get; set; }
}
