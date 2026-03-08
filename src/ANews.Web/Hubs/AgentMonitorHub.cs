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
    private static readonly Dictionary<string, string> _connections = new();

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

    // Static broadcaster - called from BaseAgent when logs are emitted
    public static IHubContext<AgentMonitorHub>? HubContext { get; set; }

    public static void RegisterHubContext(IHubContext<AgentMonitorHub> ctx)
    {
        HubContext = ctx;
        BaseAgent.OnLogEmitted += async (executionId, level, message) =>
        {
            if (HubContext != null)
            {
                await HubContext.Clients.Group("admin-monitors").SendAsync("AgentLog",
                    new { executionId, level = level.ToString(), message, timestamp = DateTime.UtcNow });
            }
        };
    }
}
