using System.Net.Http;
using System.Text;
using System.Text.Json;
using ANews.Domain.Entities;
using ANews.Domain.Enums;
using ANews.Domain.Interfaces;
using ANews.Infrastructure.AI;
using ANews.Infrastructure.Data;
using CodeHollow.FeedReader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ANews.Infrastructure.Agents;

/// <summary>
/// Runs weekly: validates existing source URLs and discovers new RSS feeds per workspace zone.
/// New sources are added as inactive — admin reviews and activates them.
/// </summary>
public class SourceDiscoveryAgent : BaseAgent
{
    protected override AgentType AgentType => AgentType.NewsScanner; // reuse enum slot
    protected override string AgentName => "SourceDiscoveryAgent";
    protected override TimeSpan DefaultInterval => TimeSpan.FromDays(7);

    public SourceDiscoveryAgent(IServiceProvider services, ILogger<SourceDiscoveryAgent> logger)
        : base(services, logger) { }

    protected override async Task RunCycleAsync(IServiceProvider services, AgentExecution execution, CancellationToken ct)
    {
        var ctx = services.GetRequiredService<AppDbContext>();
        var aiFactory = services.GetRequiredService<AiProviderFactory>();
        var httpFactory = services.GetRequiredService<IHttpClientFactory>();
        var http = httpFactory.CreateClient("rss");

        // ── Phase 1: Validate existing active sources ──────────────────
        var activeSources = await ctx.NewsSources
            .Where(s => s.IsActive && !s.IsDeleted)
            .ToListAsync(ct);

        await LogAsync(ctx, execution, AgentLogLevel.Info, $"Validando {activeSources.Count} fuentes activas");

        int fixed_ = 0, deactivated = 0;
        foreach (var src in activeSources)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var resp = await http.GetAsync(src.Url, ct);
                if ((int)resp.StatusCode is 404 or 410)
                {
                    // Try to find a redirect or corrected URL
                    var corrected = await TryFindCorrectedUrlAsync(http, src.Url, ct);
                    if (corrected != null)
                    {
                        src.Url = corrected;
                        src.LastError = null;
                        src.FailedScans = 0;
                        fixed_++;
                    }
                    else
                    {
                        src.IsActive = false;
                        src.LastError = $"URL muerta ({(int)resp.StatusCode}) — desactivada automáticamente";
                        deactivated++;
                    }
                }
                else if (resp.IsSuccessStatusCode)
                {
                    // Clear old errors if now OK
                    if (src.FailedScans > 3 && src.SuccessfulScans == 0)
                    {
                        src.LastError = null;
                        src.FailedScans = 0;
                    }
                }
            }
            catch { /* network error, skip */ }
        }

        await ctx.SaveChangesAsync(ct);
        await LogAsync(ctx, execution, AgentLogLevel.Info, $"Validación: {fixed_} corregidas, {deactivated} desactivadas");

        // ── Phase 2: Discover new sources per workspace zone ───────────
        IAiProvider ai;
        try { ai = await aiFactory.GetDefaultProviderAsync(); }
        catch
        {
            await LogAsync(ctx, execution, AgentLogLevel.Warning, "Sin proveedor IA — omitiendo descubrimiento");
            execution.ItemsProcessed = activeSources.Count;
            return;
        }

        var zones = await ctx.WorkspaceZones
            .Where(z => z.IsActive && !z.IsDeleted)
            .OrderBy(z => z.SortOrder)
            .ToListAsync(ct);

        var existingUrlsList = await ctx.NewsSources
            .Where(s => !s.IsDeleted)
            .Select(s => s.Url)
            .ToListAsync(ct);
        var existingUrls = existingUrlsList.ToHashSet();

        var sections = await ctx.NewsSections.Where(s => !s.IsDeleted).ToListAsync(ct);
        var defaultSection = sections.FirstOrDefault()?.Id ?? 1;

        int totalAdded = 0;

        foreach (var zone in zones)
        {
            if (ct.IsCancellationRequested) break;
            await LogAsync(ctx, execution, AgentLogLevel.Info, $"Descubriendo fuentes para: {zone.Flag} {zone.Name}");

            var discovered = await DiscoverForZoneAsync(ai, zone, existingUrls, ct);

            foreach (var feed in discovered)
            {
                if (ct.IsCancellationRequested) break;

                // Validate: actually fetch and confirm it's RSS
                if (!await ValidateFeedUrlAsync(http, feed.Url, ct)) continue;

                if (existingUrls.Contains(feed.Url)) continue;

                var sectionId = sections.FirstOrDefault(s =>
                    feed.Topic != null && (
                        s.Name.Contains(feed.Topic, StringComparison.OrdinalIgnoreCase) ||
                        feed.Topic.Contains(s.Name, StringComparison.OrdinalIgnoreCase)))?.Id
                    ?? defaultSection;

                ctx.NewsSources.Add(new NewsSource
                {
                    Name = feed.Name,
                    Url = feed.Url,
                    Type = NewsSourceType.Rss,
                    Language = feed.Language ?? "es",
                    CredibilityScore = feed.Credibility,
                    NewsSectionId = sectionId,
                    IsActive = false  // always inactive — admin reviews
                });

                existingUrls.Add(feed.Url);
                totalAdded++;
            }

            await ctx.SaveChangesAsync(ct);
        }

        execution.ItemsProcessed = activeSources.Count;
        execution.ItemsCreated = totalAdded;
        await LogAsync(ctx, execution, AgentLogLevel.Info,
            $"Descubrimiento completo: {totalAdded} fuentes nuevas añadidas (inactivas, pendientes de revisión)");
    }

    private static async Task<string?> TryFindCorrectedUrlAsync(HttpClient http, string originalUrl, CancellationToken ct)
    {
        // Try common RSS path variations
        var uri = new Uri(originalUrl);
        var baseUrl = $"{uri.Scheme}://{uri.Host}";
        var candidates = new[]
        {
            $"{baseUrl}/rss/",
            $"{baseUrl}/feed/",
            $"{baseUrl}/rss.xml",
            $"{baseUrl}/feed.xml",
            $"{baseUrl}/atom.xml",
            $"{baseUrl}/news/rss",
        };

        foreach (var candidate in candidates)
        {
            try
            {
                var resp = await http.GetAsync(candidate, ct);
                if (resp.IsSuccessStatusCode)
                {
                    var content = await resp.Content.ReadAsStringAsync(ct);
                    if (content.Contains("<rss") || content.Contains("<feed") || content.Contains("<atom"))
                        return candidate;
                }
            }
            catch { }
        }
        return null;
    }

    private static async Task<bool> ValidateFeedUrlAsync(HttpClient http, string url, CancellationToken ct)
    {
        try
        {
            var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return false;
            var content = await resp.Content.ReadAsStringAsync(ct);
            return content.Contains("<rss") || content.Contains("<feed") || content.Contains("<atom") || content.Contains("<channel");
        }
        catch { return false; }
    }

    private async Task<List<DiscoveredFeed>> DiscoverForZoneAsync(
        IAiProvider ai, WorkspaceZone zone, HashSet<string> existingUrls, CancellationToken ct)
    {
        var systemPrompt =
            "You are a journalism expert with encyclopedic knowledge of RSS/Atom feeds worldwide. " +
            "You only list feeds that are CURRENTLY ACTIVE and publicly accessible. " +
            "You always provide the exact, full RSS/Atom URL — not the homepage. " +
            "Respond ONLY with valid JSON, no markdown.";

        var exampleJson =
            "{\"sources\":[{\"name\":\"El País\",\"url\":\"https://feeds.elpais.com/mrss-s/pages/ep/site/elpais.com/portada\",\"language\":\"es\",\"credibility\":95,\"topic\":\"General\"}]}";

        var prompt =
            $"List 25 diverse RSS/Atom feeds covering news about {zone.Name} ({string.Join(", ", zone.GeoTerms.Take(5))}). " +
            "Include: national newspapers, TV channels, radio, regional press, sports, culture, economy, politics. " +
            "Mix mainstream and independent sources. Only include feeds with publicly accessible RSS. " +
            $"\nRespond ONLY with JSON:\n{exampleJson}";

        var response = await ai.CompleteAsync(new AiRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = prompt,
            MaxTokens = 3000,
            Temperature = 0.4,
            OperationTag = "source_discovery"
        }, ct);

        if (!response.Success || string.IsNullOrEmpty(response.Content)) return [];
        return ParseDiscoveryResponse(response.Content, existingUrls);
    }

    private static List<DiscoveredFeed> ParseDiscoveryResponse(string content, HashSet<string> existingUrls)
    {
        var result = new List<DiscoveredFeed>();
        try
        {
            var start = content.IndexOf('{');
            var end = content.LastIndexOf('}');
            if (start < 0 || end <= start) return result;

            using var doc = JsonDocument.Parse(content[start..(end + 1)]);
            if (!doc.RootElement.TryGetProperty("sources", out var arr)) return result;

            foreach (var el in arr.EnumerateArray())
            {
                var url = el.TryGetProperty("url", out var u) ? u.GetString() : null;
                var name = el.TryGetProperty("name", out var n) ? n.GetString() : null;
                if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(name)) continue;
                url = url.Trim();
                if (!url.StartsWith("http") || existingUrls.Contains(url)) continue;

                result.Add(new DiscoveredFeed(
                    name,
                    url,
                    el.TryGetProperty("language", out var l) ? l.GetString() : "es",
                    el.TryGetProperty("credibility", out var c) ? Math.Clamp(c.GetInt32(), 40, 100) : 75,
                    el.TryGetProperty("topic", out var t) ? t.GetString() : null
                ));
            }
        }
        catch { }
        return result;
    }

    record DiscoveredFeed(string Name, string Url, string? Language, int Credibility, string? Topic);
}
