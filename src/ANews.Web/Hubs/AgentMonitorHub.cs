using ANews.Domain.Enums;
using ANews.Infrastructure.Agents;
using ANews.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ANews.Web.Hubs;

[Authorize(Policy = "RequireAdmin")]
public class AgentMonitorHub : Hub
{
    private readonly AppDbContext _ctx;

    public AgentMonitorHub(AppDbContext ctx) => _ctx = ctx;

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "admin-monitors");
        var recent = await GetRecentExecutionsAsync();
        await Clients.Caller.SendAsync("RecentExecutions", recent);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "admin-monitors");
        await base.OnDisconnectedAsync(exception);
    }

    public async Task GetExecutionLogs(int executionId)
    {
        var logs = await _ctx.AgentLogs
            .Where(l => l.AgentExecutionId == executionId)
            .OrderBy(l => l.Timestamp)
            .Select(l => new { l.Level, l.Message, l.Timestamp, l.Data })
            .ToListAsync();

        await Clients.Caller.SendAsync("ExecutionLogs", executionId, logs);
    }

    private async Task<object> GetRecentExecutionsAsync()
    {
        return await _ctx.AgentExecutions
            .OrderByDescending(e => e.StartedAt)
            .Take(20)
            .Select(e => new
            {
                e.Id, e.AgentName, e.AgentType, e.Status,
                e.StartedAt, e.CompletedAt,
                e.ItemsProcessed, e.ItemsCreated, e.AiCost,
                e.Error,
                DurationMs = e.CompletedAt.HasValue
                    ? (int)(e.CompletedAt.Value - e.StartedAt).TotalMilliseconds
                    : -1
            })
            .ToListAsync();
    }

    // Wire up the DI-based event bus to broadcast to SignalR clients
    public static void RegisterHubContext(IHubContext<AgentMonitorHub> hubCtx, IAgentEventBus eventBus)
    {
        eventBus.OnLogEmitted += async (executionId, level, message) =>
        {
            await hubCtx.Clients.Group("admin-monitors").SendAsync("AgentLog",
                new { executionId, level = level.ToString(), message, timestamp = DateTime.UtcNow });
        };
    }
}
