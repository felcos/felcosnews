using ANews.Domain.Enums;

namespace ANews.Domain.Entities;

public class AiProviderConfig : BaseEntity
{
    public required string Name { get; set; }
    public AiProviderType Provider { get; set; }
    public required string Model { get; set; }
    public required string EncryptedApiKey { get; set; }
    public string? BaseUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public decimal CostPerInputTokenK { get; set; } = 0;
    public decimal CostPerOutputTokenK { get; set; } = 0;
    public int MaxTokens { get; set; } = 4096;
    public int RateLimitPerMinute { get; set; } = 60;
    public int TotalRequestsToday { get; set; } = 0;
    public int TotalRequestsMonth { get; set; } = 0;
    public decimal TotalCostToday { get; set; } = 0;
    public decimal TotalCostMonth { get; set; } = 0;
    public decimal MonthlyBudgetLimit { get; set; } = 0;
    public DateTime? LastUsedAt { get; set; }
    public DateTime? LastResetDailyAt { get; set; }
    public string? ExtraConfig { get; set; }

    public ICollection<CostEntry> CostEntries { get; set; } = [];
    public ICollection<AgentExecution> AgentExecutions { get; set; } = [];
}
