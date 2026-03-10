namespace ANews.Domain.Entities;

public class AgentConfig : BaseEntity
{
    public required string AgentType { get; set; }
    public int IntervalMinutes { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int? MaxItemsPerCycle { get; set; }
    public string? Notes { get; set; }
}
