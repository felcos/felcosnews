using ANews.Domain.Entities;
using ANews.Domain.Enums;
using ANews.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

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

        // ── Widget Endpoint ──────────────────────────────────────────────────

        app.MapGet("/api/widget/events", async (int? limit, string? section, AppDbContext ctx, IConfiguration config) =>
        {
            var take = Math.Clamp(limit ?? 10, 1, 50);
            var appUrl = config["AppUrl"] ?? "https://news.websoftware.es";
            var now = DateTime.UtcNow;

            var query = ctx.NewsEvents
                .Include(e => e.Section)
                .Where(e => e.IsActive && !e.IsDeleted && e.EventType == "Detected");

            if (!string.IsNullOrWhiteSpace(section))
                query = query.Where(e => e.Section!.Slug == section);

            var events = await query
                .OrderByDescending(e => e.CreatedAt)
                .Take(take)
                .Select(e => new
                {
                    e.Id, e.Title, e.Description,
                    Section = e.Section!.Name,
                    SectionSlug = e.Section.Slug,
                    Priority = e.Priority.ToString(),
                    e.ImpactScore, e.Location, e.CreatedAt
                })
                .ToListAsync();

            var result = events.Select(e =>
            {
                var span = now - e.CreatedAt;
                var timeAgo = span.TotalMinutes < 1 ? "hace un momento"
                    : span.TotalMinutes < 60 ? $"hace {(int)span.TotalMinutes} min"
                    : span.TotalHours < 24 ? $"hace {(int)span.TotalHours}h"
                    : span.TotalDays < 30 ? $"hace {(int)span.TotalDays}d"
                    : $"hace {(int)(span.TotalDays / 30)}m";

                return new { e.Id, e.Title, e.Description, e.Section, e.SectionSlug, e.Priority,
                    e.ImpactScore, e.Location, TimeAgo = timeAgo, e.CreatedAt,
                    Url = $"{appUrl}/?eventId={e.Id}" };
            });

            return Results.Json(result);
        }).AllowAnonymous().RequireRateLimiting("api").RequireCors("Widget");

        // ── Public API v1 ────────────────────────────────────────────────────

        app.MapGet("/api/v1/events", async (int? page, int? pageSize, string? section,
            string? priority, DateTime? since, string? q, AppDbContext ctx) =>
        {
            var pg = Math.Max(page ?? 1, 1);
            var ps = Math.Clamp(pageSize ?? 20, 1, 100);

            var query = ctx.NewsEvents
                .Include(e => e.Section)
                .Where(e => e.IsActive && !e.IsDeleted && e.EventType == "Detected");

            if (!string.IsNullOrWhiteSpace(section))
                query = query.Where(e => e.Section!.Slug == section);
            if (!string.IsNullOrWhiteSpace(priority) && Enum.TryParse<EventPriority>(priority, true, out var p))
                query = query.Where(e => e.Priority == p);
            if (since.HasValue)
                query = query.Where(e => e.CreatedAt >= since.Value);
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(e => e.Title.Contains(q));

            var total = await query.CountAsync();
            var totalPages = (int)Math.Ceiling((double)total / ps);

            var events = await query
                .OrderByDescending(e => e.CreatedAt)
                .Skip((pg - 1) * ps).Take(ps)
                .Select(e => new
                {
                    e.Id, e.Title, e.Description,
                    Section = e.Section!.Name,
                    Priority = e.Priority.ToString(),
                    e.ImpactScore, e.Location, e.Latitude, e.Longitude,
                    Trend = e.Trend.ToString(),
                    e.SourceDiversity, e.CrossReferenceCount, e.StoryThreadId,
                    e.StartDate, e.CreatedAt
                })
                .ToListAsync();

            return Results.Json(new { Data = events, Total = total, Page = pg, PageSize = ps, TotalPages = totalPages });
        }).AllowAnonymous().RequireRateLimiting("api");

        app.MapGet("/api/v1/sections", async (AppDbContext ctx) =>
        {
            var sections = await ctx.NewsSections
                .Where(s => !s.IsDeleted && s.IsPublic)
                .Select(s => new
                {
                    s.Id, s.Name, s.Slug, s.Description, Icon = s.IconClass,
                    EventCount = s.Events.Count(e => e.IsActive && !e.IsDeleted && e.EventType == "Detected")
                })
                .ToListAsync();
            return Results.Json(sections);
        }).AllowAnonymous().RequireRateLimiting("api");

        app.MapGet("/api/v1/story-threads", async (string? status, int? limit, AppDbContext ctx) =>
        {
            var take = Math.Clamp(limit ?? 20, 1, 50);
            var query = ctx.StoryThreads
                .Include(t => t.PrimarySection)
                .Where(t => !t.IsDeleted);

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<StoryStatus>(status, true, out var s))
                query = query.Where(t => t.Status == s);

            var threads = await query
                .OrderByDescending(t => t.LastEventDate)
                .Take(take)
                .Select(t => new
                {
                    t.Id, t.Title, t.Summary, t.WhyItMatters, t.WhatToWatch,
                    Status = t.Status.ToString(), t.KeyActors, t.Tags,
                    MaxPriority = t.MaxPriority.ToString(),
                    t.EventCount, t.TotalArticles, t.FirstEventDate, t.LastEventDate,
                    PrimarySection = t.PrimarySection != null ? t.PrimarySection.Name : null
                })
                .ToListAsync();

            return Results.Json(new { Data = threads });
        }).AllowAnonymous().RequireRateLimiting("api");

        app.MapGet("/api/v1/morning-brief", async (AppDbContext ctx) =>
        {
            var brief = await ctx.MorningBriefs
                .Where(b => !b.IsDeleted)
                .OrderByDescending(b => b.BriefDate)
                .FirstOrDefaultAsync();

            if (brief == null)
                return Results.NotFound(new { error = "No morning brief available" });

            return Results.Json(new
            {
                brief.Id, brief.BriefDate, brief.Headline, brief.TopStories,
                brief.DeepDive, brief.Developing, brief.Surprise, brief.GeneratedAt
            });
        }).AllowAnonymous().RequireRateLimiting("api");

        app.MapGet("/api/v1/events/{id}/briefing", async (int id, AppDbContext ctx) =>
        {
            var briefing = await ctx.EventBriefings
                .Where(b => b.NewsEventId == id && !b.IsDeleted)
                .OrderByDescending(b => b.GeneratedAt)
                .FirstOrDefaultAsync();

            if (briefing == null)
                return Results.NotFound(new { error = "No briefing available for this event" });

            return Results.Json(new
            {
                briefing.Id, briefing.Title, briefing.WhyItMatters, briefing.Background,
                briefing.KeyActors, briefing.WhatToWatch, briefing.GeneratedAt,
                briefing.SourceArticleCount
            });
        }).AllowAnonymous().RequireRateLimiting("api");

        // ── Activity Tracking ────────────────────────────────────────────────

        app.MapPost("/api/activity/track", async (HttpContext http, AppDbContext ctx,
            UserManager<ApplicationUser> userMgr) =>
        {
            var user = await userMgr.GetUserAsync(http.User);
            if (user == null) return Results.Unauthorized();

            ActivityTrackRequest? req;
            try { req = await http.Request.ReadFromJsonAsync<ActivityTrackRequest>(); }
            catch { return Results.BadRequest(); }
            if (req == null) return Results.BadRequest();

            if (!Enum.TryParse<ActivityType>(req.Type, true, out var actType))
                return Results.BadRequest(new { error = "Invalid activity type" });

            var activity = new UserActivity
            {
                UserId = user.Id,
                ActivityType = actType,
                NewsSectionId = req.SectionId,
                NewsEventId = req.EventId,
                NewsArticleId = req.ArticleId,
                StoryThreadId = req.ThreadId,
                Metadata = req.Metadata,
                CreatedAt = DateTime.UtcNow
            };
            ctx.UserActivities.Add(activity);

            // Increment section quota if applicable
            if (req.SectionId.HasValue && (actType == ActivityType.EventOpened || actType == ActivityType.ArticleRead))
            {
                var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var quota = await ctx.SectionQuotas
                    .FirstOrDefaultAsync(q => q.UserId == user.Id && q.NewsSectionId == req.SectionId.Value);

                if (quota != null)
                {
                    if (quota.PeriodStart < monthStart)
                    {
                        quota.CurrentReads = 0;
                        quota.PeriodStart = monthStart;
                    }
                    quota.CurrentReads++;

                    if (quota.MaxReadsPerMonth > 0 && quota.CurrentReads > quota.MaxReadsPerMonth)
                        return Results.Json(new { tracked = true, quotaExceeded = true,
                            current = quota.CurrentReads, limit = quota.MaxReadsPerMonth }, statusCode: 429);
                }
            }

            await ctx.SaveChangesAsync();
            return Results.Ok(new { tracked = true });
        }).RequireAuthorization().RequireRateLimiting("api");
    }
}

public record ActivityTrackRequest
{
    public required string Type { get; init; }
    public int? SectionId { get; init; }
    public int? EventId { get; init; }
    public int? ArticleId { get; init; }
    public int? ThreadId { get; init; }
    public string? Metadata { get; init; }
}
