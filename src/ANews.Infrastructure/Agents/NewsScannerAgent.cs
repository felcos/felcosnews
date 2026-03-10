using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using ANews.Domain.Entities;
using ANews.Domain.Enums;
using ANews.Infrastructure.AI;
using ANews.Infrastructure.Data;
using CodeHollow.FeedReader;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ANews.Infrastructure.Agents;

public class NewsScannerAgent : BaseAgent
{
    protected override AgentType AgentType => AgentType.NewsScanner;
    protected override string AgentName => "NewsScannerAgent";
    protected override TimeSpan DefaultInterval => TimeSpan.FromHours(1);

    public NewsScannerAgent(IServiceProvider services, ILogger<NewsScannerAgent> logger)
        : base(services, logger) { }

    protected override async Task RunCycleAsync(IServiceProvider services, AgentExecution execution, CancellationToken ct)
    {
        var ctx = services.GetRequiredService<AppDbContext>();

        var sources = await ctx.NewsSources
            .Include(s => s.Section)
            .Where(s => s.IsActive && !s.IsDeleted)
            .ToListAsync(ct);

        await LogAsync(ctx, execution, AgentLogLevel.Info, $"Escaneando {sources.Count} fuentes");

        int totalNew = 0;

        var httpFactory = services.GetRequiredService<IHttpClientFactory>();

        foreach (var source in sources)
        {
            try
            {
                var newArticles = await ScanSourceAsync(ctx, httpFactory, source, ct);
                totalNew += newArticles;
                source.LastScannedAt = DateTime.UtcNow;
                source.SuccessfulScans++;
                source.TotalArticlesFound += newArticles;
                await LogAsync(ctx, execution, AgentLogLevel.Info, $"[{source.Name}] {newArticles} articulos nuevos");
            }
            catch (Exception ex)
            {
                source.FailedScans++;
                source.LastError = ex.Message;
                _logger.LogWarning(ex, "Error escaneando fuente {Source}", source.Name);
                await LogAsync(ctx, execution, AgentLogLevel.Warning, $"[{source.Name}] Error: {ex.Message}");
            }

            await ctx.SaveChangesAsync(ct);
        }

        execution.ItemsProcessed = sources.Count;
        execution.ItemsCreated = totalNew;
        await LogAsync(ctx, execution, AgentLogLevel.Info, $"Scan completo: {totalNew} articulos nuevos de {sources.Count} fuentes");
    }

    private async Task<int> ScanSourceAsync(AppDbContext ctx, IHttpClientFactory httpFactory, NewsSource source, CancellationToken ct)
    {
        var http = httpFactory.CreateClient("rss");

        var response = await http.GetAsync(source.Url, ct);
        response.EnsureSuccessStatusCode();

        // Read bytes and detect encoding from content-type or BOM
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var charset = response.Content.Headers.ContentType?.CharSet;
        Encoding encoding;
        try { encoding = string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset); }
        catch { encoding = Encoding.UTF8; }
        var content = encoding.GetString(bytes);

        // If content is HTML (not RSS), skip
        if (content.TrimStart().StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
            content.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("La URL devuelve HTML, no RSS. Verifica la URL del feed.");

        // Sanitize common XML issues before parsing
        content = SanitizeXml(content);

        var feed = FeedReader.ReadFromString(content);
        int newCount = 0;

        foreach (var item in feed.Items.Take(50))
        {
            if (string.IsNullOrWhiteSpace(item.Link)) continue;

            // Deduplication by URL hash
            var hash = ComputeHash(item.Link);
            if (await ctx.NewsArticles.AnyAsync(a => a.ContentHash == hash, ct))
                continue;

            // Create unclassified article (EventDetector will assign to events)
            var unclassifiedEvent = await GetOrCreateUnclassifiedEventAsync(ctx, source.NewsSectionId);

            var article = new NewsArticle
            {
                Title = item.Title ?? "Sin titulo",
                Summary = StripHtml(item.Description ?? ""),
                SourceUrl = item.Link,
                SourceName = source.Name,
                PublishedAt = item.PublishingDate ?? DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow,
                Language = source.Language,
                ContentHash = hash,
                CredibilityScore = source.CredibilityScore,
                NewsEventId = unclassifiedEvent.Id,
                NewsSourceId = source.Id
            };

            ctx.NewsArticles.Add(article);
            newCount++;
        }

        await ctx.SaveChangesAsync(ct);
        return newCount;
    }

    private async Task<NewsEvent> GetOrCreateUnclassifiedEventAsync(AppDbContext ctx, int sectionId)
    {
        var today = DateTime.UtcNow.Date;
        var existing = await ctx.NewsEvents
            .FirstOrDefaultAsync(e => e.NewsSectionId == sectionId
                                   && e.EventType == "Unclassified"
                                   && e.StartDate >= today);
        if (existing != null) return existing;

        var ev = new NewsEvent
        {
            Title = "Articulos sin clasificar",
            EventType = "Unclassified",
            Priority = EventPriority.Low,
            NewsSectionId = sectionId,
            StartDate = DateTime.UtcNow,
            IsActive = false
        };
        ctx.NewsEvents.Add(ev);
        await ctx.SaveChangesAsync();
        return ev;
    }

    private static string SanitizeXml(string xml)
    {
        // Remove undeclared namespace prefixes (common in some feeds like xlink)
        xml = Regex.Replace(xml, @"\s+xlink:\w+=""[^""]*""", "");
        xml = Regex.Replace(xml, @"\s+xlink:\w+='[^']*'", "");
        // Replace unescaped & not followed by known entity or #
        xml = Regex.Replace(xml, @"&(?!(?:amp|lt|gt|quot|apos|#\d+|#x[\da-fA-F]+);)", "&amp;");
        // Remove CDATA end sequences that appear outside CDATA
        xml = xml.Replace("]]>", "");
        return xml;
    }

    private static string ComputeHash(string url)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(bytes);
    }

    private static string StripHtml(string html)
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);
        return doc.DocumentNode.InnerText.Trim();
    }
}
