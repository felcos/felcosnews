using ANews.Domain.Entities;
using ANews.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ANews.Web.Endpoints;

public static class PublicApiEndpoints
{
    public static void MapPublicApiEndpoints(this WebApplication app)
    {
        app.MapGet("/health/detail", async (AppDbContext db) =>
        {
            var now = DateTime.UtcNow;
            var last24h = now.AddHours(-24);
            var agentStats = await db.AgentExecutions
                .Where(e => e.StartedAt >= last24h)
                .GroupBy(e => e.AgentType)
                .Select(g => new {
                    agent = g.Key.ToString(),
                    runs = g.Count(),
                    lastRun = g.Max(e => e.StartedAt),
                    errors = g.Count(e => e.Status == ANews.Domain.Enums.AgentStatus.Failed),
                    totalCost = g.Sum(e => e.AiCost)
                })
                .ToListAsync();
            var eventCount  = await db.NewsEvents.CountAsync(e => !e.IsDeleted && e.IsActive);
            var articleCount = await db.NewsArticles.CountAsync(a => !a.IsDeleted);
            return Results.Json(new { status = "ok", utc = now, events = eventCount, articles = articleCount, agents = agentStats });
        }).AllowAnonymous();

        app.MapGet("/api/user/module-keywords", async (HttpContext http, AppDbContext ctx, UserManager<ApplicationUser> userMgr) =>
        {
            var user = await userMgr.GetUserAsync(http.User);
            if (user == null) return Results.Ok(new string[0]);

            var keywords = await ctx.Set<ModuleKeyword>()
                .Where(k => k.Module != null && k.Module.UserId == user.Id && k.Module.IsActive && !k.Module.IsDeleted)
                .Select(k => k.Keyword)
                .Distinct()
                .ToListAsync();

            return Results.Ok(keywords);
        }).RequireAuthorization();

        app.MapGet("/api/rss/{token}", async (string token, AppDbContext ctx, IConfiguration config) =>
        {
            var module = await ctx.UserModules
                .Include(m => m.Keywords)
                .FirstOrDefaultAsync(m => m.RssFeedToken == token && m.IsActive && !m.IsDeleted);

            if (module == null)
                return Results.NotFound();

            var keywords = module.Keywords.Select(k => k.Keyword.ToLower()).ToList();

            var events = await ctx.NewsEvents
                .Where(e => e.IsActive && e.EventType != "Unclassified")
                .OrderByDescending(e => e.CreatedAt)
                .Take(50)
                .ToListAsync();

            if (keywords.Count > 0)
            {
                events = events.Where(e =>
                {
                    var text = $"{e.Title} {e.Description} {string.Join(" ", e.Tags)}".ToLowerInvariant();
                    return keywords.Any(kw => text.Contains(kw));
                }).ToList();
            }

            var appUrl = config["AppUrl"] ?? "https://news.websoftware.es";
            var xml = new System.Text.StringBuilder();
            xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
            xml.AppendLine("<rss version=\"2.0\" xmlns:atom=\"http://www.w3.org/2005/Atom\">");
            xml.AppendLine("<channel>");
            xml.AppendLine($"<title>AgenteNews - {System.Net.WebUtility.HtmlEncode(module.Name)}</title>");
            xml.AppendLine($"<link>{appUrl}</link>");
            xml.AppendLine($"<description>{System.Net.WebUtility.HtmlEncode(module.Description ?? module.Name)}</description>");
            xml.AppendLine($"<language>es</language>");
            xml.AppendLine($"<atom:link href=\"{appUrl}/api/rss/{token}\" rel=\"self\" type=\"application/rss+xml\"/>");

            foreach (var ev in events)
            {
                xml.AppendLine("<item>");
                xml.AppendLine($"<title>{System.Net.WebUtility.HtmlEncode(ev.Title)}</title>");
                xml.AppendLine($"<link>{appUrl}/?event={ev.Id}</link>");
                xml.AppendLine($"<description>{System.Net.WebUtility.HtmlEncode(ev.Description ?? ev.Title)}</description>");
                xml.AppendLine($"<pubDate>{ev.CreatedAt:R}</pubDate>");
                xml.AppendLine($"<guid>{appUrl}/?event={ev.Id}</guid>");
                xml.AppendLine("</item>");
            }

            xml.AppendLine("</channel>");
            xml.AppendLine("</rss>");

            return Results.Content(xml.ToString(), "application/rss+xml; charset=utf-8");
        });
    }
}
