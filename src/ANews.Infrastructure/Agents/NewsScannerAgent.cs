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
    protected override TimeSpan DefaultInterval => TimeSpan.FromHours(6);

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

        // Group by domain to throttle requests per site
        var byDomain = sources.GroupBy(s =>
        {
            try { return new Uri(s.Url).Host.ToLowerInvariant(); }
            catch { return "unknown"; }
        }).ToDictionary(g => g.Key, g => g.ToList());

        var allSourcesOrdered = byDomain.Values
            .SelectMany(g => g.Select((s, i) => (Source: s, DomainIndex: i)))
            .OrderBy(x => x.DomainIndex) // Interleave domains to avoid hammering same site
            .ThenBy(x => x.Source.Name)
            .Select(x => x.Source);

        foreach (var source in allSourcesOrdered)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                int newArticles = source.Type switch
                {
                    NewsSourceType.Gdelt => await ScanGdeltAsync(ctx, httpFactory, source, ct),
                    NewsSourceType.WorldNewsApi => await ScanWorldNewsApiAsync(ctx, httpFactory, source, ct),
                    NewsSourceType.Scraper => await ScanHtmlAsync(ctx, httpFactory, source, ct),
                    _ => await ScanSourceAsync(ctx, httpFactory, source, ct)
                };
                totalNew += newArticles;
                source.LastScannedAt = DateTime.UtcNow;
                source.SuccessfulScans++;
                source.TotalArticlesFound += newArticles;
                source.LastError = null; // Clear error on success
                await LogAsync(ctx, execution, AgentLogLevel.Info, $"[{source.Name}] {newArticles} articulos nuevos");
            }
            catch (Exception ex)
            {
                source.FailedScans++;
                source.LastError = ex.Message;
                source.LastScannedAt = DateTime.UtcNow; // Marcar como escaneada aunque falle
                _logger.LogWarning(ex, "Error escaneando fuente {Source}", source.Name);
                await LogAsync(ctx, execution, AgentLogLevel.Warning, $"[{source.Name}] Error: {ex.Message}");
            }

            await ctx.SaveChangesAsync(ct);

            // Small delay to avoid rate limiting (429)
            await Task.Delay(200, ct);
        }

        execution.ItemsProcessed = sources.Count;
        execution.ItemsCreated = totalNew;
        await LogAsync(ctx, execution, AgentLogLevel.Info, $"Scan completo: {totalNew} articulos nuevos de {sources.Count} fuentes");
    }

    private async Task<int> ScanSourceAsync(AppDbContext ctx, IHttpClientFactory httpFactory, NewsSource source, CancellationToken ct)
    {
        var http = httpFactory.CreateClient("rss");

        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(source.Url, ct);
        }
        catch (HttpRequestException ex) when (ex.Message.Contains("301") || ex.Message.Contains("302"))
        {
            // Manual redirect fallback — should not happen with AllowAutoRedirect but just in case
            return 0;
        }

        // Retry 403 with alternative headers (Referer trick)
        if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            using HttpRequestMessage retryRequest = new HttpRequestMessage(HttpMethod.Get, source.Url);
            retryRequest.Headers.Add("Referer", new Uri(source.Url).GetLeftPart(UriPartial.Authority) + "/");
            retryRequest.Headers.Add("Sec-Fetch-Dest", "document");
            retryRequest.Headers.Add("Sec-Fetch-Mode", "navigate");
            retryRequest.Headers.Add("Sec-Fetch-Site", "same-origin");
            retryRequest.Headers.Add("Sec-Fetch-User", "?1");
            retryRequest.Headers.Add("Upgrade-Insecure-Requests", "1");
            response = await http.SendAsync(retryRequest, ct);
        }

        response.EnsureSuccessStatusCode();

        // Read bytes and detect encoding from content-type or BOM
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var charset = response.Content.Headers.ContentType?.CharSet;
        Encoding encoding;
        try { encoding = string.IsNullOrEmpty(charset) ? Encoding.UTF8 : Encoding.GetEncoding(charset); }
        catch { encoding = Encoding.UTF8; }
        var content = encoding.GetString(bytes);

        // If content is HTML (not RSS), fallback to HTML scraping
        string trimmed = content.TrimStart();
        if (trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase))
            return await ScanHtmlFromContentAsync(ctx, source, content);

        // Try JSON Feed format (starts with { and has "items" key)
        if (trimmed.StartsWith("{"))
            return await ScanJsonFeedAsync(ctx, source, content, ct);

        // Sanitize common XML issues before parsing
        content = SanitizeXml(content);

        // Try to parse RSS/Atom — if it fails, fallback to HTML scraping
        CodeHollow.FeedReader.Feed feed;
        try
        {
            feed = FeedReader.ReadFromString(content);
        }
        catch (Exception)
        {
            // XML parsing failed — try HTML scraping as last resort
            return await ScanHtmlFromContentAsync(ctx, source, content);
        }

        if (feed.Items.Count == 0)
            return await ScanHtmlFromContentAsync(ctx, source, content);

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

    private async Task<int> ScanGdeltAsync(AppDbContext ctx, IHttpClientFactory httpFactory, NewsSource source, CancellationToken ct)
    {
        HttpClient http = httpFactory.CreateClient("rss");

        // GDELT DOC API - source.Url contiene la query completa
        // Ejemplo: https://api.gdeltproject.org/api/v2/doc/doc?query=sourcelang:spanish&mode=artlist&maxrecords=50&format=json
        string url = source.Url;

        HttpResponseMessage response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(ct);

        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
        int newCount = 0;

        if (!doc.RootElement.TryGetProperty("articles", out System.Text.Json.JsonElement articles))
            return 0;

        foreach (System.Text.Json.JsonElement article in articles.EnumerateArray().Take(50))
        {
            string? articleUrl = article.GetProperty("url").GetString();
            if (string.IsNullOrWhiteSpace(articleUrl)) continue;

            string hash = ComputeHash(articleUrl);
            if (await ctx.NewsArticles.AnyAsync(a => a.ContentHash == hash, ct))
                continue;

            NewsEvent unclassifiedEvent = await GetOrCreateUnclassifiedEventAsync(ctx, source.NewsSectionId);

            string title = article.TryGetProperty("title", out System.Text.Json.JsonElement t) ? t.GetString() ?? "Sin titulo" : "Sin titulo";
            string domain = article.TryGetProperty("domain", out System.Text.Json.JsonElement d) ? d.GetString() ?? source.Name : source.Name;

            DateTime publishedAt = DateTime.UtcNow;
            if (article.TryGetProperty("seendate", out System.Text.Json.JsonElement sd))
            {
                string? dateStr = sd.GetString();
                if (!string.IsNullOrWhiteSpace(dateStr) && dateStr.Length >= 14)
                {
                    // Formato de fecha GDELT: YYYYMMDDHHmmSS
                    if (DateTime.TryParseExact(dateStr[..14], "yyyyMMddHHmmss",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.AssumeUniversal, out DateTime parsed))
                        publishedAt = parsed;
                }
            }

            NewsArticle newsArticle = new NewsArticle
            {
                Title = title,
                Summary = "", // GDELT no proporciona resumenes
                SourceUrl = articleUrl,
                SourceName = domain,
                PublishedAt = publishedAt,
                ProcessedAt = DateTime.UtcNow,
                Language = source.Language,
                ContentHash = hash,
                CredibilityScore = source.CredibilityScore,
                NewsEventId = unclassifiedEvent.Id,
                NewsSourceId = source.Id
            };

            ctx.NewsArticles.Add(newsArticle);
            newCount++;
        }

        await ctx.SaveChangesAsync(ct);
        return newCount;
    }

    private async Task<int> ScanWorldNewsApiAsync(AppDbContext ctx, IHttpClientFactory httpFactory, NewsSource source, CancellationToken ct)
    {
        HttpClient http = httpFactory.CreateClient("rss");

        // API key: puede estar como JSON {"apiKey":"xxx"} o como texto plano directamente
        string apiKey = "";
        if (!string.IsNullOrWhiteSpace(source.CustomHeaders))
        {
            string trimmedHeaders = source.CustomHeaders.Trim();
            if (trimmedHeaders.StartsWith("{"))
            {
                // Formato JSON
                try
                {
                    using System.Text.Json.JsonDocument headersDoc = System.Text.Json.JsonDocument.Parse(trimmedHeaders);
                    if (headersDoc.RootElement.TryGetProperty("apiKey", out System.Text.Json.JsonElement ak))
                        apiKey = ak.GetString() ?? "";
                    else if (headersDoc.RootElement.TryGetProperty("api_key", out System.Text.Json.JsonElement ak2))
                        apiKey = ak2.GetString() ?? "";
                    else if (headersDoc.RootElement.TryGetProperty("key", out System.Text.Json.JsonElement ak3))
                        apiKey = ak3.GetString() ?? "";
                }
                catch { }
            }

            // Si no se encontró en JSON o no era JSON, usar el valor directamente como API key
            if (string.IsNullOrWhiteSpace(apiKey))
                apiKey = trimmedHeaders;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("WorldNewsAPI requiere apiKey en CustomHeaders (texto plano o JSON {\"apiKey\":\"tu-key\"}).");

        // source.Url contiene la busqueda base, ej: https://api.worldnewsapi.com/search-news?language=es
        string url = source.Url;
        char separator = url.Contains('?') ? '&' : '?';
        url += $"{separator}api-key={apiKey}&number=50";

        HttpResponseMessage response = await http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        string json = await response.Content.ReadAsStringAsync(ct);

        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
        int newCount = 0;

        if (!doc.RootElement.TryGetProperty("news", out System.Text.Json.JsonElement newsArray))
            return 0;

        foreach (System.Text.Json.JsonElement item in newsArray.EnumerateArray().Take(50))
        {
            string? articleUrl = item.TryGetProperty("url", out System.Text.Json.JsonElement u) ? u.GetString() : null;
            if (string.IsNullOrWhiteSpace(articleUrl)) continue;

            string hash = ComputeHash(articleUrl);
            if (await ctx.NewsArticles.AnyAsync(a => a.ContentHash == hash, ct))
                continue;

            NewsEvent unclassifiedEvent = await GetOrCreateUnclassifiedEventAsync(ctx, source.NewsSectionId);

            string title = item.TryGetProperty("title", out System.Text.Json.JsonElement t) ? t.GetString() ?? "Sin titulo" : "Sin titulo";
            string? textContent = item.TryGetProperty("text", out System.Text.Json.JsonElement tx) ? tx.GetString() : null;
            string summary = !string.IsNullOrWhiteSpace(textContent) ? textContent[..Math.Min(textContent.Length, 500)] : "";
            string sourceName = source.Name;
            if (item.TryGetProperty("author", out System.Text.Json.JsonElement author) && !string.IsNullOrWhiteSpace(author.GetString()))
                sourceName = author.GetString()!;
            else if (item.TryGetProperty("source_country", out System.Text.Json.JsonElement sc) && !string.IsNullOrWhiteSpace(sc.GetString()))
                sourceName = sc.GetString()!;

            DateTime publishedAt = DateTime.UtcNow;
            if (item.TryGetProperty("publish_date", out System.Text.Json.JsonElement pd))
            {
                if (DateTime.TryParse(pd.GetString(), out DateTime parsed))
                    publishedAt = parsed.ToUniversalTime();
            }

            NewsArticle newsArticle = new NewsArticle
            {
                Title = title,
                Summary = summary,
                SourceUrl = articleUrl,
                SourceName = sourceName,
                PublishedAt = publishedAt,
                ProcessedAt = DateTime.UtcNow,
                Language = source.Language,
                ContentHash = hash,
                CredibilityScore = source.CredibilityScore,
                NewsEventId = unclassifiedEvent.Id,
                NewsSourceId = source.Id
            };

            ctx.NewsArticles.Add(newsArticle);
            newCount++;
        }

        await ctx.SaveChangesAsync(ct);
        return newCount;
    }

    private async Task<int> ScanHtmlAsync(AppDbContext ctx, IHttpClientFactory httpFactory, NewsSource source, CancellationToken ct)
    {
        HttpClient http = httpFactory.CreateClient("rss");
        string content = await http.GetStringAsync(source.Url, ct);
        return await ScanHtmlFromContentAsync(ctx, source, content);
    }

    private async Task<int> ScanHtmlFromContentAsync(AppDbContext ctx, NewsSource source, string html)
    {
        var doc = new HtmlAgilityPack.HtmlDocument();
        doc.LoadHtml(html);

        Uri baseUri = new Uri(source.Url);
        var candidateNodes = new List<HtmlAgilityPack.HtmlNode>();

        // XPath selectors from most specific to broadest
        string[] selectors =
        [
            // Tier 1: semantic article structures
            "//article//a[@href]",
            "//h1/a[@href] | //h2/a[@href] | //h3/a[@href] | //h4/a[@href]",
            // Also headings inside links (some sites do <a><h3>title</h3></a>)
            "//a[@href][h1 or h2 or h3 or h4]",
            // Tier 2: class-based detection
            "//a[@href][contains(@class,'title') or contains(@class,'headline') or contains(@class,'noticia') or contains(@class,'article') or contains(@class,'news') or contains(@class,'entry') or contains(@class,'story') or contains(@class,'link') or contains(@class,'enlace') or contains(@class,'titular')]",
            // Parent containers with news-related classes
            "//*[contains(@class,'article') or contains(@class,'noticia') or contains(@class,'story') or contains(@class,'card') or contains(@class,'item') or contains(@class,'post') or contains(@class,'teaser') or contains(@class,'feed') or contains(@class,'lista') or contains(@class,'news') or contains(@class,'resultado')]//a[@href]",
            // Tier 3: structural patterns
            "//main//li/a[@href] | //section//li/a[@href] | //div[@role='main']//a[@href]",
            "//a[@href][@data-title or @data-headline or @data-article-id or @data-id]",
            // Tier 4: OpenGraph/meta-based — links inside elements with itemprop
            "//*[@itemprop='headline' or @itemprop='name' or @itemprop='url']//a[@href] | //a[@href][@itemprop='url']",
            // Tier 5: broadest — any link with substantial text
            "//a[@href][string-length(normalize-space()) > 20]",
        ];

        foreach (string selector in selectors)
        {
            try
            {
                var nodes = doc.DocumentNode.SelectNodes(selector);
                if (nodes != null) candidateNodes.AddRange(nodes);
            }
            catch { /* XPath syntax error — skip */ }

            if (candidateNodes.Count >= 30) break;
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var extractedLinks = new List<(string Url, string Title)>();

        foreach (var node in candidateNodes)
        {
            string? href = node.GetAttributeValue("href", null);
            if (string.IsNullOrWhiteSpace(href)) continue;

            // Skip javascript: and mailto: links
            if (href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)) continue;
            if (href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)) continue;
            if (href.StartsWith("#")) continue;

            // Resolve relative URLs
            if (!Uri.TryCreate(baseUri, href, out Uri? fullUri)) continue;
            string fullUrl = fullUri.GetLeftPart(UriPartial.Query);

            // Skip navigation/utility links
            string path = fullUri.AbsolutePath.ToLowerInvariant();
            if (path.Length < 5) continue;
            if (path is "/" or "/index.html" or "/index.php") continue;
            if (path.Contains("/tag/") || path.Contains("/tags/")) continue;
            if (path.Contains("/categoria/") || path.Contains("/category/") || path.Contains("/categorias/")) continue;
            if (path.Contains("/author/") || path.Contains("/autor/") || path.Contains("/autores/")) continue;
            if (path.Contains("/login") || path.Contains("/register") || path.Contains("/search") || path.Contains("/contacto") || path.Contains("/contact")) continue;
            if (path.Contains("/page/") || path.Contains("/pagina/")) continue;
            if (path.Contains("/privacy") || path.Contains("/terms") || path.Contains("/legal") || path.Contains("/cookies")) continue;
            if (path.EndsWith(".jpg") || path.EndsWith(".png") || path.EndsWith(".gif") || path.EndsWith(".css") || path.EndsWith(".js") || path.EndsWith(".pdf")) continue;

            // Must be same domain or subdomain
            if (!fullUri.Host.EndsWith(baseUri.Host) && !baseUri.Host.EndsWith(fullUri.Host)) continue;

            if (!seen.Add(fullUrl)) continue;

            // Get title: prefer inner text, but also try title attribute or aria-label
            string title = node.InnerText.Trim();
            title = Regex.Replace(title, @"\s+", " ");
            if (title.Length < 10)
            {
                title = node.GetAttributeValue("title", null)?.Trim()
                     ?? node.GetAttributeValue("aria-label", null)?.Trim()
                     ?? title;
            }
            if (title.Length < 10 || title.Length > 500) continue;

            // Heuristic: URLs with date-like segments or slug patterns are more likely articles
            bool looksLikeArticle = Regex.IsMatch(path, @"/\d{4}/\d{2}/") // /2026/05/
                                 || Regex.IsMatch(path, @"/\d{4}-\d{2}-") // /2026-05-
                                 || Regex.IsMatch(path, @"/noticia[s]?/")
                                 || Regex.IsMatch(path, @"/news/")
                                 || Regex.IsMatch(path, @"/article/")
                                 || path.Split('/').Any(seg => seg.Length > 20); // Long slug segments

            // Accept if title >= 15 chars OR it looks like an article URL
            if (title.Length < 15 && !looksLikeArticle) continue;

            extractedLinks.Add((fullUrl, title));
        }

        if (extractedLinks.Count == 0)
        {
            _logger.LogWarning("HTML scraper encontró 0 artículos en {Url}, candidatos evaluados: {Count}", source.Url, candidateNodes.Count);
            return 0; // No lanzar excepción — simplemente no hay artículos esta vez
        }

        int newCount = 0;
        foreach (var (url, title) in extractedLinks.Take(50))
        {
            string hash = ComputeHash(url);
            if (await ctx.NewsArticles.AnyAsync(a => a.ContentHash == hash))
                continue;

            var unclassifiedEvent = await GetOrCreateUnclassifiedEventAsync(ctx, source.NewsSectionId);
            ctx.NewsArticles.Add(new NewsArticle
            {
                Title = StripHtml(title),
                Summary = "",
                SourceUrl = url,
                SourceName = source.Name,
                PublishedAt = DateTime.UtcNow,
                ProcessedAt = DateTime.UtcNow,
                Language = source.Language,
                ContentHash = hash,
                CredibilityScore = source.CredibilityScore,
                NewsEventId = unclassifiedEvent.Id,
                NewsSourceId = source.Id
            });
            newCount++;
        }

        await ctx.SaveChangesAsync();
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

    private async Task<int> ScanJsonFeedAsync(AppDbContext ctx, NewsSource source, string json, CancellationToken ct)
    {
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
        int newCount = 0;

        // JSON Feed spec: https://www.jsonfeed.org/version/1.1/
        System.Text.Json.JsonElement itemsElement;
        if (!doc.RootElement.TryGetProperty("items", out itemsElement))
            return 0;

        foreach (System.Text.Json.JsonElement item in itemsElement.EnumerateArray().Take(50))
        {
            string? url = item.TryGetProperty("url", out System.Text.Json.JsonElement u) ? u.GetString() : null;
            // Some JSON feeds use "external_url" instead
            if (string.IsNullOrWhiteSpace(url) && item.TryGetProperty("external_url", out System.Text.Json.JsonElement eu))
                url = eu.GetString();
            if (string.IsNullOrWhiteSpace(url)) continue;

            string hash = ComputeHash(url);
            if (await ctx.NewsArticles.AnyAsync(a => a.ContentHash == hash, ct))
                continue;

            string title = item.TryGetProperty("title", out System.Text.Json.JsonElement t) ? t.GetString() ?? "Sin titulo" : "Sin titulo";
            string summary = "";
            if (item.TryGetProperty("summary", out System.Text.Json.JsonElement s))
                summary = StripHtml(s.GetString() ?? "");
            else if (item.TryGetProperty("content_text", out System.Text.Json.JsonElement ct2))
                summary = (ct2.GetString() ?? "")[..Math.Min(ct2.GetString()?.Length ?? 0, 500)];
            else if (item.TryGetProperty("content_html", out System.Text.Json.JsonElement ch))
                summary = StripHtml(ch.GetString() ?? "")[..Math.Min(StripHtml(ch.GetString() ?? "").Length, 500)];

            DateTime publishedAt = DateTime.UtcNow;
            if (item.TryGetProperty("date_published", out System.Text.Json.JsonElement dp))
            {
                if (DateTime.TryParse(dp.GetString(), out DateTime parsed))
                    publishedAt = parsed.ToUniversalTime();
            }

            NewsEvent unclassifiedEvent = await GetOrCreateUnclassifiedEventAsync(ctx, source.NewsSectionId);
            ctx.NewsArticles.Add(new NewsArticle
            {
                Title = title,
                Summary = summary,
                SourceUrl = url,
                SourceName = source.Name,
                PublishedAt = publishedAt,
                ProcessedAt = DateTime.UtcNow,
                Language = source.Language,
                ContentHash = hash,
                CredibilityScore = source.CredibilityScore,
                NewsEventId = unclassifiedEvent.Id,
                NewsSourceId = source.Id
            });
            newCount++;
        }

        await ctx.SaveChangesAsync(ct);
        return newCount;
    }

    private static string SanitizeXml(string xml)
    {
        // Remove undeclared namespace prefixes (common in some feeds like xlink)
        xml = Regex.Replace(xml, @"\s+xlink:\w+=""[^""]*""", "");
        xml = Regex.Replace(xml, @"\s+xlink:\w+='[^']*'", "");

        // Remove control characters (except tab, newline, carriage return)
        xml = Regex.Replace(xml, @"[\x00-\x08\x0B\x0C\x0E-\x1F]", "");

        // Replace unescaped & not followed by known entity or #
        xml = Regex.Replace(xml, @"&(?!(?:amp|lt|gt|quot|apos|#\d+|#x[\da-fA-F]+);)", "&amp;");

        // Fix ALL truncated CDATA sections (not just the last one)
        int searchFrom = 0;
        while (true)
        {
            int cdataOpen = xml.IndexOf("<![CDATA[", searchFrom, StringComparison.Ordinal);
            if (cdataOpen < 0) break;

            int cdataClose = xml.IndexOf("]]>", cdataOpen + 9, StringComparison.Ordinal);
            if (cdataClose < 0)
            {
                // Unclosed CDATA at end of document — close it and patch the XML
                xml = xml + "]]></description></item></channel></rss>";
                break;
            }
            searchFrom = cdataClose + 3;
        }

        // Handle multiple root elements: wrap in a synthetic root if needed
        string trimmed = xml.TrimStart();
        if (!trimmed.StartsWith("<?xml", StringComparison.OrdinalIgnoreCase))
        {
            // Count top-level elements — if more than one, wrap
            int firstClose = trimmed.IndexOf('>');
            if (firstClose > 0)
            {
                string firstTag = trimmed[1..firstClose].Split(' ', '/')[0];
                int secondOpen = trimmed.IndexOf($"<{firstTag}", firstClose, StringComparison.OrdinalIgnoreCase);
                if (secondOpen > 0)
                {
                    // Multiple roots — wrap in synthetic element
                    xml = $"<?xml version=\"1.0\"?><_root>{xml}</_root>";
                }
            }
        }
        else
        {
            // Has XML declaration — check after it
            int declEnd = trimmed.IndexOf("?>", StringComparison.Ordinal);
            if (declEnd > 0)
            {
                string afterDecl = trimmed[(declEnd + 2)..].TrimStart();
                if (afterDecl.Length > 0 && afterDecl[0] == '<')
                {
                    int fc = afterDecl.IndexOf('>');
                    if (fc > 0)
                    {
                        string tag = afterDecl[1..fc].Split(' ', '/', '?')[0];
                        if (!string.IsNullOrEmpty(tag))
                        {
                            int secondTag = afterDecl.IndexOf($"<{tag}", fc, StringComparison.OrdinalIgnoreCase);
                            if (secondTag > 0)
                            {
                                // Multiple roots after declaration
                                string decl = trimmed[..(declEnd + 2)];
                                string body = trimmed[(declEnd + 2)..];
                                xml = $"{decl}<_root>{body}</_root>";
                            }
                        }
                    }
                }
            }
        }

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
