using ANews.Domain.Enums;

namespace ANews.Domain.Entities;

public class AgentLog : BaseEntity
{
    public int AgentExecutionId { get; set; }
    public AgentLogLevel Level { get; set; } = AgentLogLevel.Info;
    public required string Message { get; set; }
    public string? Data { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public AgentExecution Execution { get; set; } = null!;
}
