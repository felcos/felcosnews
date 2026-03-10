using ANews.Domain.Entities;
using ANews.Domain.Enums;

namespace ANews.Tests.Domain;

public class NewsEventTests
{
    [Fact]
    public void NewEvent_HasCorrectDefaults()
    {
        var ev = new NewsEvent { Title = "Test Event" };

        Assert.Equal(EventPriority.Medium, ev.Priority);
        Assert.Equal(50, ev.ImpactScore);
        Assert.True(ev.IsActive);
        Assert.Equal(EventTrend.Stable, ev.Trend);
        Assert.Empty(ev.Tags);
        Assert.Null(ev.EndDate);
    }

    [Fact]
    public void NewEvent_RequiresTitle()
    {
        var ev = new NewsEvent { Title = "Breaking News" };
        Assert.Equal("Breaking News", ev.Title);
    }

    [Theory]
    [InlineData(EventPriority.Critical)]
    [InlineData(EventPriority.High)]
    [InlineData(EventPriority.Medium)]
    [InlineData(EventPriority.Low)]
    public void Priority_AcceptsAllLevels(EventPriority priority)
    {
        var ev = new NewsEvent { Title = "Test", Priority = priority };
        Assert.Equal(priority, ev.Priority);
    }

    [Fact]
    public void Tags_CanBeSetAsArray()
    {
        var tags = new[] { "ukraine", "russia", "conflict" };
        var ev = new NewsEvent { Title = "Test", Tags = tags };

        Assert.Equal(3, ev.Tags.Length);
        Assert.Contains("ukraine", ev.Tags);
    }

    [Fact]
    public void BaseEntity_SetsTimestamps()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);
        var ev = new NewsEvent { Title = "Test" };
        var after = DateTime.UtcNow.AddSeconds(1);

        Assert.InRange(ev.CreatedAt, before, after);
        Assert.InRange(ev.UpdatedAt, before, after);
        Assert.False(ev.IsDeleted);
    }
}
