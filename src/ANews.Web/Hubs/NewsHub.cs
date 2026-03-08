using ANews.Domain.Enums;
using ANews.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ANews.Web.Hubs;

public class NewsHub : Hub
{
    private readonly AppDbContext _ctx;

    public NewsHub(AppDbContext ctx) => _ctx = ctx;

    public override async Task OnConnectedAsync()
    {
        // Send initial stats on connect
        var stats = await GetStatsAsync();
        await Clients.Caller.SendAsync("StatsUpdate", stats);
        await base.OnConnectedAsync();
    }

    public async Task JoinSection(string sectionSlug)
        => await Groups.AddToGroupAsync(Context.ConnectionId, $"section:{sectionSlug}");

    public async Task LeaveSection(string sectionSlug)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"section:{sectionSlug}");

    public async Task GetEvents(string sectionSlug, int page = 1, int size = 20)
    {
        var query = _ctx.NewsEvents
            .Include(e => e.Section)
            .Include(e => e.Articles.OrderByDescending(a => a.Relevance).Take(6))
            .Where(e => e.IsActive && e.EventType != "Unclassified");

        if (sectionSlug != "all")
            query = query.Where(e => e.Section.Slug == sectionSlug);

        var events = await query
            .OrderByDescending(e => e.Priority)
            .ThenByDescending(e => e.ImpactScore)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(e => new EventDto(
                e.Id, e.Title, e.Description, e.Priority.ToString(),
                e.ImpactScore, e.Category, e.Trend.ToString(),
                e.Tags, e.StartDate, e.Section.Name, e.Section.Slug,
                e.Articles.Select(a => new ArticleDto(
                    a.Id, a.Title, a.Summary, a.SourceUrl,
                    a.SourceName, a.Relevance, a.ArticleType.ToString()
                )).ToList()
            ))
            .ToListAsync();

        await Clients.Caller.SendAsync("EventsLoaded", events);
    }

    private async Task<object> GetStatsAsync()
    {
        var today = DateTime.UtcNow.Date;
        return new
        {
            ActiveEvents = await _ctx.NewsEvents.CountAsync(e => e.IsActive && e.EventType != "Unclassified"),
            ArticlesToday = await _ctx.NewsArticles.CountAsync(a => a.CreatedAt >= today),
            ActiveSections = await _ctx.NewsSections.CountAsync(s => s.IsPublic),
            PendingAlerts = await _ctx.AlertTriggers.CountAsync(a => !a.IsAcknowledged)
        };
    }
}

// Broadcaster for real-time updates (called from agents/controllers)
public class NewsBroadcaster
{
    private readonly IHubContext<NewsHub> _hub;
    public NewsBroadcaster(IHubContext<NewsHub> hub) => _hub = hub;

    public async Task BroadcastNewEventAsync(string sectionSlug, object eventData)
        => await _hub.Clients.Group($"section:{sectionSlug}").SendAsync("NewEvent", eventData);

    public async Task BroadcastCriticalAlertAsync(object alert)
        => await _hub.Clients.All.SendAsync("CriticalAlert", alert);

    public async Task BroadcastStatsUpdateAsync(object stats)
        => await _hub.Clients.All.SendAsync("StatsUpdate", stats);
}

record EventDto(int Id, string Title, string? Description, string Priority,
    decimal ImpactScore, string? Category, string Trend, string[] Tags,
    DateTime StartDate, string SectionName, string SectionSlug,
    List<ArticleDto> Articles);

record ArticleDto(int Id, string Title, string? Summary, string SourceUrl,
    string SourceName, decimal Relevance, string ArticleType);
