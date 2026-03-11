using System.Text;
using ANews.Domain.Entities;
using ANews.Domain.Enums;
using ANews.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace ANews.Infrastructure.Agents;

public class TelegramEditorialAgent : BaseAgent
{
    protected override AgentType AgentType => AgentType.TelegramEditorial;
    protected override string AgentName => "TelegramEditorialAgent";
    protected override TimeSpan DefaultInterval => TimeSpan.FromMinutes(30);

    private readonly ITelegramBotClient? _bot;
    private readonly string? _channelId;

    public TelegramEditorialAgent(IServiceProvider services, ILogger<TelegramEditorialAgent> logger)
        : base(services, logger)
    {
        var config = services.GetRequiredService<IConfiguration>();
        var token = config["Telegram:BotToken"];
        _channelId = config["Telegram:EditorialChannelId"];

        if (!string.IsNullOrEmpty(token))
            _bot = new TelegramBotClient(token);
        else
            _logger.LogWarning("Telegram:BotToken not configured. Editorial channel posting disabled.");
    }

    protected override async Task RunCycleAsync(IServiceProvider services, AgentExecution execution, CancellationToken ct)
    {
        if (_bot == null || string.IsNullOrEmpty(_channelId))
        {
            await LogAsync(
                services.GetRequiredService<AppDbContext>(), execution, AgentLogLevel.Warning,
                "Telegram bot or editorial channel not configured. Skipping cycle.");
            return;
        }

        var ctx = services.GetRequiredService<AppDbContext>();
        var itemsProcessed = 0;

        // Phase 1: Morning Brief
        itemsProcessed += await SendMorningBriefIfNeeded(ctx, execution, ct);

        // Phase 2: Breaking News
        itemsProcessed += await SendBreakingNewsIfNeeded(ctx, execution, ct);

        execution.ItemsProcessed = itemsProcessed;
    }

    // ─── Phase 1: Morning Brief ──────────────────────────────────────────

    private async Task<int> SendMorningBriefIfNeeded(AppDbContext ctx, AgentExecution execution, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        var brief = await ctx.MorningBriefs
            .Where(b => b.BriefDate == today)
            .OrderByDescending(b => b.GeneratedAt)
            .FirstOrDefaultAsync(ct);

        if (brief == null)
            return 0;

        // Check if we already sent the morning brief today
        var alreadySent = await ctx.AgentLogs
            .AnyAsync(l => l.Message == "morning_brief_sent"
                && l.Timestamp >= today
                && l.Execution.AgentType == AgentType, ct);

        if (alreadySent)
            return 0;

        var html = FormatMorningBrief(brief);

        try
        {
            await _bot!.SendMessage(_channelId!, html, parseMode: ParseMode.Html, cancellationToken: ct);
            await LogAsync(ctx, execution, AgentLogLevel.Info, "morning_brief_sent",
                $"BriefId={brief.Id}, Date={brief.BriefDate:yyyy-MM-dd}");
            _logger.LogInformation("[{Agent}] Morning brief sent to {Channel}", AgentName, _channelId);
            return 1;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Agent}] Failed to send morning brief to Telegram", AgentName);
            await LogAsync(ctx, execution, AgentLogLevel.Error,
                $"Failed to send morning brief: {ex.Message}");
            return 0;
        }
    }

    private static string FormatMorningBrief(MorningBrief brief)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"☀️ <b>{EscapeHtml(brief.Headline)}</b>");
        sb.AppendLine($"📅 {brief.BriefDate:MMMM dd, yyyy}");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(brief.TopStories))
        {
            sb.AppendLine("📰 <b>Top Stories</b>");
            sb.AppendLine(EscapeHtml(brief.TopStories));
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(brief.DeepDive))
        {
            sb.AppendLine("🔍 <b>Deep Dive</b>");
            sb.AppendLine(EscapeHtml(brief.DeepDive));
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(brief.Developing))
        {
            sb.AppendLine("⏳ <b>Developing</b>");
            sb.AppendLine(EscapeHtml(brief.Developing));
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(brief.Surprise))
        {
            sb.AppendLine("🎲 <b>Surprise</b>");
            sb.AppendLine(EscapeHtml(brief.Surprise));
            sb.AppendLine();
        }

        sb.Append("🌐 <a href=\"https://news.websoftware.es\">Read more at AgenteNews</a>");

        return sb.ToString();
    }

    // ─── Phase 2: Breaking News ──────────────────────────────────────────

    private async Task<int> SendBreakingNewsIfNeeded(AppDbContext ctx, AgentExecution execution, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddMinutes(-30);

        var breakingEvents = await ctx.NewsEvents
            .Include(e => e.Section)
            .Where(e => e.Priority >= EventPriority.Critical
                && e.ImpactScore >= 70
                && e.CreatedAt >= cutoff
                && e.IsActive)
            .OrderByDescending(e => e.ImpactScore)
            .ToListAsync(ct);

        if (breakingEvents.Count == 0)
            return 0;

        var sent = 0;

        foreach (var ev in breakingEvents)
        {
            // Check if this event was already broadcast
            var alreadyBroadcast = await ctx.AgentLogs
                .AnyAsync(l => l.Message == $"breaking_news_sent:{ev.Id}"
                    && l.Execution.AgentType == AgentType, ct);

            if (alreadyBroadcast)
                continue;

            var html = FormatBreakingNews(ev);

            try
            {
                await _bot!.SendMessage(_channelId!, html, parseMode: ParseMode.Html, cancellationToken: ct);
                await LogAsync(ctx, execution, AgentLogLevel.Info,
                    $"breaking_news_sent:{ev.Id}",
                    $"Title={ev.Title}, Priority={ev.Priority}, Impact={ev.ImpactScore}");
                _logger.LogInformation("[{Agent}] Breaking news sent: {Title}", AgentName, ev.Title);
                sent++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Agent}] Failed to send breaking news for event {EventId}", AgentName, ev.Id);
                await LogAsync(ctx, execution, AgentLogLevel.Error,
                    $"Failed to send breaking news for event {ev.Id}: {ex.Message}");
            }
        }

        return sent;
    }

    private static string FormatBreakingNews(NewsEvent ev)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"🔴 <b>BREAKING: {EscapeHtml(ev.Title)}</b>");
        sb.AppendLine();

        if (!string.IsNullOrWhiteSpace(ev.Description))
        {
            sb.AppendLine(EscapeHtml(ev.Description));
            sb.AppendLine();
        }

        if (!string.IsNullOrWhiteSpace(ev.Location))
            sb.AppendLine($"📍 {EscapeHtml(ev.Location)}");

        sb.AppendLine($"⚡ Impact: {ev.ImpactScore}/100");
        sb.AppendLine();
        sb.Append($"🔗 <a href=\"https://news.websoftware.es/?eventId={ev.Id}\">Full coverage</a>");

        return sb.ToString();
    }

    // ─── Helpers ─────────────────────────────────────────────────────────

    private static string EscapeHtml(string text)
        => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
