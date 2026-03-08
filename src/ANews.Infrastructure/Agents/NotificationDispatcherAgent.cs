using ANews.Domain.Entities;
using ANews.Domain.Enums;
using ANews.Domain.Interfaces;
using ANews.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ANews.Infrastructure.Agents;

public class NotificationDispatcherAgent : BaseAgent
{
    protected override AgentType AgentType => AgentType.NotificationDispatcher;
    protected override string AgentName => "NotificationDispatcherAgent";
    protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

    public NotificationDispatcherAgent(IServiceProvider services, ILogger<NotificationDispatcherAgent> logger)
        : base(services, logger) { }

    protected override async Task RunCycleAsync(IServiceProvider services, AgentExecution execution, CancellationToken ct)
    {
        var ctx = services.GetRequiredService<AppDbContext>();
        var notificationServices = services.GetServices<INotificationService>().ToList();

        // Get new events from last 10 minutes to match against user modules
        var cutoff = DateTime.UtcNow.AddMinutes(-10);
        var newEvents = await ctx.NewsEvents
            .Where(e => e.IsActive && e.CreatedAt >= cutoff && e.EventType != "Unclassified")
            .ToListAsync(ct);

        if (newEvents.Count == 0) return;

        // Get active user modules with notifications enabled
        var modules = await ctx.UserModules
            .Include(m => m.Keywords)
            .Where(m => m.IsActive && m.NotificationsEnabled)
            .ToListAsync(ct);

        int dispatched = 0;

        foreach (var module in modules)
        {
            var matchingEvents = newEvents.Where(e => EventMatchesModule(e, module)).ToList();
            if (matchingEvents.Count == 0) continue;

            // Get user's active notification channels
            var channels = await ctx.NotificationChannels
                .Where(c => c.UserId == module.UserId && c.IsActive && c.IsVerified)
                .ToListAsync(ct);

            foreach (var ev in matchingEvents)
            {
                foreach (var channel in channels)
                {
                    var svc = notificationServices.FirstOrDefault(s => s.ChannelType == channel.Type);
                    if (svc == null) continue;

                    var message = new NotificationMessage
                    {
                        Title = $"[{ev.Priority}] {ev.Title}",
                        Body = ev.Description ?? ev.Title,
                        ChannelConfig = channel.Config,
                        UserId = module.UserId,
                        NewsEventId = ev.Id
                    };

                    var sent = await svc.SendAsync(message, ct);

                    ctx.NotificationLogs.Add(new NotificationLog
                    {
                        UserId = module.UserId,
                        NotificationChannelId = channel.Id,
                        UserModuleId = module.Id,
                        NewsEventId = ev.Id,
                        Message = message.Body,
                        Sent = sent,
                        Error = sent ? null : "Error al enviar",
                        SentAt = DateTime.UtcNow
                    });

                    if (sent)
                    {
                        channel.TotalSent++;
                        channel.LastUsedAt = DateTime.UtcNow;
                        dispatched++;
                    }
                    else
                    {
                        channel.TotalFailed++;
                    }
                }

                module.TotalMatchedEvents++;
                module.LastMatchAt = DateTime.UtcNow;
            }
        }

        await ctx.SaveChangesAsync(ct);
        execution.ItemsProcessed = modules.Count;
        execution.ItemsCreated = dispatched;

        if (dispatched > 0)
            await LogAsync(ctx, execution, AgentLogLevel.Info, $"Enviadas {dispatched} notificaciones");
    }

    private static bool EventMatchesModule(NewsEvent ev, UserModule module)
    {
        if (module.Keywords.Count == 0) return false;

        var searchText = $"{ev.Title} {ev.Description} {string.Join(" ", ev.Tags)}".ToLowerInvariant();

        return module.Keywords.Any(kw =>
            kw.IsExact
                ? searchText.Contains($" {kw.Keyword.ToLower()} ")
                : searchText.Contains(kw.Keyword.ToLower()));
    }
}
