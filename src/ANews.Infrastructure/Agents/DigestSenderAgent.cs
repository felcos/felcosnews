using ANews.Domain.Entities;
using ANews.Domain.Enums;
using ANews.Infrastructure.Data;
using ANews.Infrastructure.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ANews.Infrastructure.Agents;

public class DigestSenderAgent : BaseAgent
{
    protected override AgentType AgentType => AgentType.DigestSender;
    protected override string AgentName => "DigestSenderAgent";
    protected override TimeSpan DefaultInterval => TimeSpan.FromHours(1);

    public DigestSenderAgent(IServiceProvider services, ILogger<DigestSenderAgent> logger)
        : base(services, logger) { }

    protected override async Task RunCycleAsync(IServiceProvider services, AgentExecution execution, CancellationToken ct)
    {
        var ctx = services.GetRequiredService<AppDbContext>();
        var emailSender = services.GetRequiredService<IdentityEmailSender>();

        var now = DateTime.UtcNow;

        // Load users that opted in to digest and have at least one active module
        var usersWithDigest = await ctx.Users
            .Where(u => u.IsActive && u.ReceiveDigest && u.Email != null)
            .ToListAsync(ct);

        if (usersWithDigest.Count == 0)
        {
            await LogAsync(ctx, execution, AgentLogLevel.Info, "No hay usuarios con digest activado");
            return;
        }

        int sent = 0;

        foreach (var user in usersWithDigest)
        {
            if (ct.IsCancellationRequested) break;

            // Determine if it's time for this user's digest
            var windowStart = GetWindowStart(user.DigestFrequency, user.LastDigestSentAt, now);
            if (windowStart == null) continue;

            try
            {
                // Load user modules with keywords
                var modules = await ctx.UserModules
                    .Include(m => m.Keywords)
                    .Where(m => m.UserId == user.Id && m.IsActive)
                    .ToListAsync(ct);

                if (modules.Count == 0) continue;

                // Find events since last digest that match any module keyword
                var allKeywords = modules
                    .SelectMany(m => m.Keywords.Select(k => k.Keyword.ToLower()))
                    .Distinct()
                    .ToList();

                var events = await ctx.NewsEvents
                    .Include(e => e.Section)
                    .Where(e => e.IsActive && e.CreatedAt >= windowStart)
                    .OrderByDescending(e => e.ImpactScore)
                    .Take(50)
                    .ToListAsync(ct);

                // Filter by keyword relevance
                var matched = events
                    .Where(e => allKeywords.Any(kw =>
                        e.Title.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
                        (e.Description != null && e.Description.Contains(kw, StringComparison.OrdinalIgnoreCase)) ||
                        (e.Tags != null && e.Tags.Any(t => t.Contains(kw, StringComparison.OrdinalIgnoreCase)))))
                    .Take(10)
                    .ToList();

                if (matched.Count == 0) continue;

                // Try to include morning brief for daily digests
                MorningBrief? morningBrief = null;
                if (user.DigestFrequency == DigestionFrequency.Daily)
                {
                    morningBrief = await ctx.Set<MorningBrief>()
                        .Where(m => m.BriefDate == now.Date)
                        .FirstOrDefaultAsync(ct);
                }

                // Build and send digest email
                var subject = BuildSubject(user.DigestFrequency, matched.Count);
                var body = BuildHtmlBody(user.DisplayName, user.DigestFrequency, matched, windowStart.Value, now, morningBrief);

                await emailSender.SendRawEmailAsync(user.Email!, subject, body);

                // Update last sent timestamp
                user.LastDigestSentAt = now;
                await ctx.SaveChangesAsync(ct);

                sent++;
                await LogAsync(ctx, execution, AgentLogLevel.Info,
                    $"Digest enviado a {user.Email} ({matched.Count} eventos)");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error enviando digest a {Email}", user.Email);
                await LogAsync(ctx, execution, AgentLogLevel.Warning,
                    $"Error al enviar digest a {user.Email}: {ex.Message}");
            }
        }

        execution.ItemsProcessed = usersWithDigest.Count;
        execution.ItemsCreated = sent;
        await LogAsync(ctx, execution, AgentLogLevel.Info, $"Digests enviados: {sent}/{usersWithDigest.Count}");
    }

    private static DateTime? GetWindowStart(DigestionFrequency freq, DateTime? lastSent, DateTime now)
    {
        var minInterval = freq switch
        {
            DigestionFrequency.Daily => TimeSpan.FromHours(20),
            DigestionFrequency.Weekly => TimeSpan.FromDays(6),
            DigestionFrequency.Monthly => TimeSpan.FromDays(28),
            _ => TimeSpan.FromHours(20)
        };

        if (lastSent.HasValue && (now - lastSent.Value) < minInterval)
            return null;

        return freq switch
        {
            DigestionFrequency.Daily => now.AddDays(-1),
            DigestionFrequency.Weekly => now.AddDays(-7),
            DigestionFrequency.Monthly => now.AddDays(-30),
            _ => now.AddDays(-1)
        };
    }

    private static string BuildSubject(DigestionFrequency freq, int count)
    {
        var period = freq switch
        {
            DigestionFrequency.Daily => "diario",
            DigestionFrequency.Weekly => "semanal",
            DigestionFrequency.Monthly => "mensual",
            _ => "periódico"
        };
        return $"[AgenteNews] Tu resumen {period} — {count} evento{(count != 1 ? "s" : "")} relevante{(count != 1 ? "s" : "")}";
    }

    private static string BuildHtmlBody(string displayName, DigestionFrequency freq,
        List<NewsEvent> events, DateTime from, DateTime to, MorningBrief? morningBrief = null)
    {
        var period = freq switch
        {
            DigestionFrequency.Daily => "últimas 24 horas",
            DigestionFrequency.Weekly => "última semana",
            DigestionFrequency.Monthly => "último mes",
            _ => "último período"
        };

        // CSS kept in plain string to avoid conflicts with $"" brace escaping
        const string css = """
            body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; background: #0a0e1a; color: #c0c8e0; margin: 0; padding: 24px; }
            .container { max-width: 600px; margin: 0 auto; }
            .header { background: #111827; border-left: 4px solid #4a90e2; padding: 20px 24px; border-radius: 8px 8px 0 0; }
            .header h1 { margin: 0; font-size: 1.4em; color: #fff; }
            .header p { margin: 4px 0 0; color: #8899bb; font-size: 0.9em; }
            .event-card { background: #111827; border: 1px solid #1e2a3a; border-radius: 6px; padding: 16px; margin: 12px 0; }
            .event-title { font-weight: 600; color: #e8eaf0; font-size: 1em; margin-bottom: 6px; }
            .event-meta { font-size: 0.8em; color: #6b7a99; margin-bottom: 8px; }
            .event-desc { font-size: 0.88em; color: #9aaabb; line-height: 1.5; }
            .priority-critical { border-left: 3px solid #ff0040; }
            .priority-high { border-left: 3px solid #ff8c42; }
            .priority-medium { border-left: 3px solid #4a90e2; }
            .priority-low { border-left: 3px solid #34495e; }
            .footer { text-align: center; padding: 20px; color: #4a5568; font-size: 0.8em; }
            .btn { display: inline-block; background: #4a90e2; color: #fff; padding: 8px 18px; border-radius: 6px; text-decoration: none; font-size: 0.85em; margin-top: 10px; }
            """;

        var sb = new System.Text.StringBuilder();
        sb.Append($"""
            <!DOCTYPE html>
            <html lang="es">
            <head><meta charset="utf-8"><title>Digest AgenteNews</title>
            <style>{css}</style>
            </head>
            <body>
            <div class="container">
                <div class="header">
                    <h1>AgenteNews — Resumen {period}</h1>
                    <p>Hola, {System.Net.WebUtility.HtmlEncode(displayName)}. Aquí tienes los eventos relevantes del {from:dd/MM} al {to:dd/MM/yyyy}.</p>
                </div>
            """);

        // Morning Brief editorial section
        if (morningBrief != null)
        {
            sb.Append($"""
                <div style="background:#0d1525;border:1px solid #1e2a3a;border-radius:8px;padding:20px;margin:16px 0 20px;">
                    <div style="font-size:10px;font-weight:700;color:#4a90e2;letter-spacing:1px;margin-bottom:10px;">BRIEFING DEL DÍA</div>
                    <div style="font-size:1.1em;color:#fff;font-weight:600;margin-bottom:14px;">{System.Net.WebUtility.HtmlEncode(morningBrief.Headline)}</div>
                    <div style="font-size:0.88em;color:#9aaabb;line-height:1.6;">{System.Net.WebUtility.HtmlEncode(morningBrief.TopStories).Replace("\n", "<br/>")}</div>
                </div>
                """);
        }

        foreach (var ev in events)
        {
            var priorityClass = ev.Priority.ToString().ToLower();
            var desc = string.IsNullOrEmpty(ev.Description)
                ? ""
                : $"<p class=\"event-desc\">{System.Net.WebUtility.HtmlEncode(ev.Description[..Math.Min(ev.Description.Length, 250)])}</p>";

            sb.Append($"""
                <div class="event-card priority-{priorityClass}">
                    <div class="event-title">{System.Net.WebUtility.HtmlEncode(ev.Title)}</div>
                    <div class="event-meta">{ev.Section?.Name} · {ev.Priority} · {ev.CreatedAt:dd/MM HH:mm} UTC · Impacto: {ev.ImpactScore:F1}</div>
                    {desc}
                </div>
                """);
        }

        sb.Append($"""
                <div style="text-align:center;padding:20px;">
                    <a href="https://news.websoftware.es" class="btn">Ver todas las noticias</a>
                </div>
                <div class="footer">
                    Recibes este resumen porque tienes el digest activado en AgenteNews.<br>
                    Puedes desactivarlo en <a href="https://news.websoftware.es/user/profile" style="color:#4a90e2">Mi Perfil</a>.
                </div>
            </div>
            </body>
            </html>
            """);

        return sb.ToString();
    }
}
