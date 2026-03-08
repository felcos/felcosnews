using ANews.Domain.Enums;

namespace ANews.Domain.Entities;

public class AgentExecution : BaseEntity
{
    public AgentType AgentType { get; set; }
    public required string AgentName { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public AgentStatus Status { get; set; } = AgentStatus.Running;
    public string? Error { get; set; }
    public int ItemsProcessed { get; set; } = 0;
    public int ItemsCreated { get; set; } = 0;
    public int ItemsUpdated { get; set; } = 0;
    public decimal AiCost { get; set; } = 0;
    public int? AiProviderConfigId { get; set; }
    public string? TriggerReason { get; set; }

    public AiProviderConfig? AiProvider { get; set; }
    public ICollection<AgentLog> Logs { get; set; } = [];

    public TimeSpan? Duration => CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;
}
