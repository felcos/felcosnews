namespace ANews.Domain.Entities;

public class AuditLog : BaseEntity
{
    public int UserId { get; set; }
    public required string UserEmail { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public string? EntityId { get; set; }
    public string? OldValues { get; set; }
    public string? NewValues { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
