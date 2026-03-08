using ANews.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ANews.Web.Hubs;

[Authorize(Policy = "RequireAdmin")]
public class AdminHub : Hub
{
    private readonly AppDbContext _ctx;

    public AdminHub(AppDbContext ctx) => _ctx = ctx;

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "admins");
        var dashboard = await GetDashboardStatsAsync();
        await Clients.Caller.SendAsync("DashboardStats", dashboard);
        await base.OnConnectedAsync();
    }

    public async Task RefreshStats()
    {
        var stats = await GetDashboardStatsAsync();
        await Clients.Caller.SendAsync("DashboardStats", stats);
    }

    private async Task<object> GetDashboardStatsAsync()
    {
        var now = DateTime.UtcNow;
        var today = now.Date;
        var thisMonth = new DateTime(now.Year, now.Month, 1);

        var runningAgents = await _ctx.AgentExecutions
            .Where(e => e.Status == ANews.Domain.Enums.AgentStatus.Running)
            .Select(e => new { e.AgentName, e.StartedAt })
            .ToListAsync();

        var costToday = await _ctx.CostEntries
            .Where(c => c.Date >= today)
            .SumAsync(c => c.Cost);

        var costMonth = await _ctx.CostEntries
            .Where(c => c.Date >= thisMonth)
            .SumAsync(c => c.Cost);

        var costByProvider = await _ctx.CostEntries
            .Where(c => c.Date >= today)
            .GroupBy(c => c.Provider.Name)
            .Select(g => new { Provider = g.Key, Cost = g.Sum(c => c.Cost) })
            .ToListAsync();

        return new
        {
            RunningAgents = runningAgents,
            AgentsRunningCount = runningAgents.Count,
            ArticlesToday = await _ctx.NewsArticles.CountAsync(a => a.CreatedAt >= today),
            EventsToday = await _ctx.NewsEvents.CountAsync(e => e.CreatedAt >= today && e.EventType != "Unclassified"),
            ActiveUsers = await _ctx.Users.CountAsync(u => u.IsActive),
            TotalUsers = await _ctx.Users.CountAsync(),
            CostToday = costToday,
            CostMonth = costMonth,
            CostByProvider = costByProvider,
            PendingAlerts = await _ctx.AlertTriggers.CountAsync(a => !a.IsAcknowledged),
            ActiveSections = await _ctx.NewsSections.CountAsync(s => !s.IsDeleted),
            ActiveSources = await _ctx.NewsSources.CountAsync(s => s.IsActive),
            LastUpdateAt = now
        };
    }
}
