using ANews.Domain.Enums;

namespace ANews.Domain.Entities;

public class SectionQuota : BaseEntity
{
    public int UserId { get; set; }
    public int NewsSectionId { get; set; }
    public int MaxReadsPerMonth { get; set; } = -1; // -1 = unlimited
    public int CurrentReads { get; set; } = 0;
    public DateTime PeriodStart { get; set; } = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    public PlanTier PlanTier { get; set; } = PlanTier.Free;

    public NewsSection? Section { get; set; }
}
