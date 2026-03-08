using ANews.Domain.Enums;

namespace ANews.Domain.Entities;

public class AlertTrigger : BaseEntity
{
    public int NewsEventId { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public EventPriority Severity { get; set; }
    public bool IsAcknowledged { get; set; } = false;
    public int? AcknowledgedByUserId { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
    public bool NotificationSent { get; set; } = false;

    public NewsEvent NewsEvent { get; set; } = null!;
}
