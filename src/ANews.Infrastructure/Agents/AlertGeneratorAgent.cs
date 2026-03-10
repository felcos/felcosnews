using ANews.Domain.Entities;
using ANews.Domain.Enums;
using ANews.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ANews.Infrastructure.Agents;

public class AlertGeneratorAgent : BaseAgent
{
    protected override AgentType AgentType => AgentType.AlertGenerator;
    protected override string AgentName => "AlertGeneratorAgent";
    protected override TimeSpan DefaultInterval => TimeSpan.FromMinutes(15);

    public AlertGeneratorAgent(IServiceProvider services, ILogger<AlertGeneratorAgent> logger)
        : base(services, logger) { }

    protected override async Task RunCycleAsync(IServiceProvider services, AgentExecution execution, CancellationToken ct)
    {
        var ctx = services.GetRequiredService<AppDbContext>();

        // Detect events that should generate alerts (new Critical/High events without alerts)
        var cutoff = DateTime.UtcNow.AddHours(-1);
        var eventsNeedingAlerts = await ctx.NewsEvents
            .Include(e => e.AlertTriggers)
            .Where(e => e.IsActive
                     && e.CreatedAt >= cutoff
                     && (e.Priority == EventPriority.Critical || e.Priority == EventPriority.High)
                     && !e.AlertTriggers.Any(a => !a.IsAcknowledged))
            .ToListAsync(ct);

        foreach (var ev in eventsNeedingAlerts)
        {
            var alert = new AlertTrigger
            {
                NewsEventId = ev.Id,
                Title = $"[{ev.Priority}] {ev.Title}",
                Message = ev.Description ?? ev.Title,
                Severity = ev.Priority,
                IsAcknowledged = false,
                NotificationSent = false
            };

            ctx.AlertTriggers.Add(alert);
        }

        await ctx.SaveChangesAsync(ct);
        execution.ItemsCreated = eventsNeedingAlerts.Count;

        if (eventsNeedingAlerts.Count > 0)
            await LogAsync(ctx, execution, AgentLogLevel.Info, $"Generadas {eventsNeedingAlerts.Count} alertas criticas");

        // Phase A: Lifecycle management — auto-archive inactive events
        var archiveCutoff = DateTime.UtcNow.AddDays(-14);
        var staleEvents = await ctx.NewsEvents
            .Where(e => e.IsActive && e.EventType == "Detected"
                     && e.UpdatedAt < archiveCutoff)
            .ToListAsync(ct);

        foreach (var ev in staleEvents)
        {
            ev.IsActive = false;
            ev.EventType = "Archived";
        }

        if (staleEvents.Count > 0)
        {
            await ctx.SaveChangesAsync(ct);
            await LogAsync(ctx, execution, AgentLogLevel.Info, $"Archivados {staleEvents.Count} eventos inactivos (>14 dias)");
        }
    }
}
