namespace ANews.Domain.Entities;

public class CostEntry : BaseEntity
{
    public int AiProviderConfigId { get; set; }
    public int? AgentExecutionId { get; set; }
    public required string Operation { get; set; }
    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public decimal Cost { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;

    public AiProviderConfig Provider { get; set; } = null!;
    public AgentExecution? AgentExecution { get; set; }
}
