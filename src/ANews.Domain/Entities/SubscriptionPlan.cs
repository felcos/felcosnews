namespace ANews.Domain.Entities;

public class SubscriptionPlan : BaseEntity
{
    public required string Name { get; set; }
    public string? Description { get; set; }
    public ANews.Domain.Enums.PlanTier Tier { get; set; } = ANews.Domain.Enums.PlanTier.Free;
    public decimal MonthlyPrice { get; set; } = 0;
    public Dictionary<string, int> SectionLimits { get; set; } = new(); // sectionSlug → maxReads/month, -1=unlimited
    public int DefaultMaxReadsPerMonth { get; set; } = -1; // -1 = unlimited
    public string[] Features { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}
