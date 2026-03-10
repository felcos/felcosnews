using ANews.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ANews.Tests.Web;

public class DatabaseSeederTests
{
    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"SeederTest_{Guid.NewGuid()}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void AppDbContext_SoftDeleteFilter_ExcludesDeleted()
    {
        using var ctx = CreateContext();

        ctx.NewsSections.Add(new ANews.Domain.Entities.NewsSection
        {
            Name = "Active", Slug = "active", IsDeleted = false, IsPublic = true
        });
        ctx.NewsSections.Add(new ANews.Domain.Entities.NewsSection
        {
            Name = "Deleted", Slug = "deleted", IsDeleted = true, IsPublic = true
        });
        ctx.SaveChanges();

        var visible = ctx.NewsSections.ToList();
        Assert.Single(visible);
        Assert.Equal("Active", visible[0].Name);
    }

    [Fact]
    public void AppDbContext_IgnoreQueryFilters_IncludesDeleted()
    {
        using var ctx = CreateContext();

        ctx.NewsSections.Add(new ANews.Domain.Entities.NewsSection
        {
            Name = "Active", Slug = "active", IsDeleted = false, IsPublic = true
        });
        ctx.NewsSections.Add(new ANews.Domain.Entities.NewsSection
        {
            Name = "Deleted", Slug = "deleted", IsDeleted = true, IsPublic = true
        });
        ctx.SaveChanges();

        var all = ctx.NewsSections.IgnoreQueryFilters().ToList();
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void AppDbContext_UpdateTimestamps_SetsCreatedAt()
    {
        using var ctx = CreateContext();

        var before = DateTime.UtcNow.AddSeconds(-1);
        ctx.NewsSections.Add(new ANews.Domain.Entities.NewsSection
        {
            Name = "Test", Slug = "test", IsPublic = true
        });
        ctx.SaveChanges();
        var after = DateTime.UtcNow.AddSeconds(1);

        var section = ctx.NewsSections.First();
        Assert.InRange(section.CreatedAt, before, after);
    }

    [Fact]
    public void AppDbContext_UpdateTimestamps_SetsUpdatedAtOnModify()
    {
        using var ctx = CreateContext();

        ctx.NewsSections.Add(new ANews.Domain.Entities.NewsSection
        {
            Name = "Original", Slug = "test", IsPublic = true
        });
        ctx.SaveChanges();

        var section = ctx.NewsSections.First();
        var originalUpdated = section.UpdatedAt;

        // Small delay to ensure different timestamp
        Thread.Sleep(50);
        section.Name = "Modified";
        ctx.SaveChanges();

        Assert.True(section.UpdatedAt >= originalUpdated);
    }
}
