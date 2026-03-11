using ANews.Domain.Entities;
using ANews.Domain.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ANews.Infrastructure.Data;

public class ApplicationUser : IdentityUser<int>
{
    public required string DisplayName { get; set; }
    public string? AvatarUrl { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool ReceiveDigest { get; set; } = false;
    public DigestionFrequency DigestFrequency { get; set; } = DigestionFrequency.Daily;
    public DateTime? LastDigestSentAt { get; set; }
    public string? ApiTokenPrefix { get; set; }
}

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<NewsSection> NewsSections => Set<NewsSection>();
    public DbSet<NewsSource> NewsSources => Set<NewsSource>();
    public DbSet<NewsEvent> NewsEvents => Set<NewsEvent>();
    public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();
    public DbSet<UserModule> UserModules => Set<UserModule>();
    public DbSet<ModuleKeyword> ModuleKeywords => Set<ModuleKeyword>();
    public DbSet<NotificationChannel> NotificationChannels => Set<NotificationChannel>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<AiProviderConfig> AiProviderConfigs => Set<AiProviderConfig>();
    public DbSet<AgentExecution> AgentExecutions => Set<AgentExecution>();
    public DbSet<AgentLog> AgentLogs => Set<AgentLog>();
    public DbSet<CostEntry> CostEntries => Set<CostEntry>();
    public DbSet<AlertTrigger> AlertTriggers => Set<AlertTrigger>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();
    public DbSet<ApiToken> ApiTokens => Set<ApiToken>();
    public DbSet<WorkspaceZone> WorkspaceZones => Set<WorkspaceZone>();
    public DbSet<AgentConfig> AgentConfigs => Set<AgentConfig>();
    public DbSet<StoryThread> StoryThreads => Set<StoryThread>();
    public DbSet<EventBriefing> EventBriefings => Set<EventBriefing>();
    public DbSet<MorningBrief> MorningBriefs => Set<MorningBrief>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Rename Identity tables
        builder.Entity<ApplicationUser>().ToTable("users");
        builder.Entity<IdentityRole<int>>().ToTable("roles");
        builder.Entity<IdentityUserRole<int>>().ToTable("user_roles");
        builder.Entity<IdentityUserClaim<int>>().ToTable("user_claims");
        builder.Entity<IdentityUserLogin<int>>().ToTable("user_logins");
        builder.Entity<IdentityRoleClaim<int>>().ToTable("role_claims");
        builder.Entity<IdentityUserToken<int>>().ToTable("user_tokens");

        builder.Entity<NewsSection>(e =>
        {
            e.HasIndex(s => s.Slug).IsUnique();
            e.Property(s => s.Keywords).HasColumnType("jsonb");
        });

        builder.Entity<NewsEvent>(e =>
        {
            e.Property(ev => ev.Tags).HasColumnType("jsonb");
            e.HasIndex(ev => ev.NewsSectionId);
            e.HasIndex(ev => new { ev.Priority, ev.ImpactScore });
            e.HasIndex(ev => ev.CreatedAt);
            e.HasOne(ev => ev.ParentEvent)
             .WithMany(ev => ev.RelatedEvents)
             .HasForeignKey(ev => ev.ParentEventId)
             .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<NewsArticle>(e =>
        {
            e.Property(a => a.Keywords).HasColumnType("jsonb");
            e.HasIndex(a => a.ContentHash);
            e.HasIndex(a => a.PublishedAt);
            e.HasIndex(a => a.NewsEventId);
            e.Property(a => a.Relevance).HasPrecision(5, 2);
            e.Property(a => a.SentimentScore).HasPrecision(5, 2);
            e.Property(a => a.CredibilityScore).HasPrecision(5, 2);
        });

        builder.Entity<AiProviderConfig>(e =>
        {
            e.Property(a => a.CostPerInputTokenK).HasPrecision(10, 6);
            e.Property(a => a.CostPerOutputTokenK).HasPrecision(10, 6);
            e.Property(a => a.TotalCostToday).HasPrecision(10, 4);
            e.Property(a => a.TotalCostMonth).HasPrecision(10, 4);
            e.Property(a => a.MonthlyBudgetLimit).HasPrecision(10, 4);
        });

        builder.Entity<CostEntry>(e =>
        {
            e.Property(c => c.Cost).HasPrecision(10, 6);
            e.HasIndex(c => new { c.AiProviderConfigId, c.Date });
        });

        builder.Entity<AgentLog>(e =>
        {
            e.HasIndex(l => l.AgentExecutionId);
            e.HasIndex(l => l.Timestamp);
        });

        builder.Entity<Bookmark>(e =>
        {
            e.HasIndex(b => new { b.UserId, b.NewsArticleId }).IsUnique();
        });

        builder.Entity<ApiToken>(e =>
        {
            e.HasIndex(t => t.TokenHash).IsUnique();
        });

        builder.Entity<WorkspaceZone>(e =>
        {
            e.Property(w => w.GeoTerms).HasColumnType("jsonb");
        });

        builder.Entity<AgentConfig>(e =>
        {
            e.HasIndex(a => a.AgentType).IsUnique();
        });

        builder.Entity<StoryThread>(e =>
        {
            e.Property(s => s.KeyActors).HasColumnType("jsonb");
            e.Property(s => s.Tags).HasColumnType("jsonb");
            e.HasIndex(s => s.Status);
            e.HasIndex(s => s.LastEventDate);
        });

        builder.Entity<NewsEvent>(e2 =>
        {
            e2.HasOne(ev => ev.StoryThread)
              .WithMany(st => st.Events)
              .HasForeignKey(ev => ev.StoryThreadId)
              .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<EventBriefing>(e =>
        {
            e.HasIndex(b => b.NewsEventId);
            e.HasIndex(b => b.StoryThreadId);
            e.HasIndex(b => b.Type);
        });

        builder.Entity<MorningBrief>(e =>
        {
            e.HasIndex(m => m.BriefDate).IsUnique();
        });

        builder.Entity<NewsSource>(e =>
        {
            e.Property(s => s.FactDensityAvg).HasPrecision(5, 2);
        });

        // Global query filter for soft delete
        builder.Entity<NewsSection>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<NewsSource>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<NewsEvent>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<NewsArticle>().HasQueryFilter(e => !e.IsDeleted);
        builder.Entity<UserModule>().HasQueryFilter(e => !e.IsDeleted);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(ct);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries<BaseEntity>()
            .Where(e => e.State is EntityState.Added or EntityState.Modified);

        foreach (var entry in entries)
        {
            entry.Entity.UpdatedAt = DateTime.UtcNow;
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = DateTime.UtcNow;
        }
    }
}
