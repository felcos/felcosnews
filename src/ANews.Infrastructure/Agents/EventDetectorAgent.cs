using System.Net.Http;
using System.Text.Json;
using ANews.Domain.Entities;
using ANews.Domain.Enums;
using ANews.Domain.Interfaces;
using ANews.Infrastructure.AI;
using ANews.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ANews.Infrastructure.Agents;

public class EventDetectorAgent : BaseAgent
{
    protected override AgentType AgentType => AgentType.EventDetector;
    protected override string AgentName => "EventDetectorAgent";
    protected override TimeSpan Interval => TimeSpan.FromHours(2);

    public EventDetectorAgent(IServiceProvider services, ILogger<EventDetectorAgent> logger)
        : base(services, logger) { }

    protected override async Task RunCycleAsync(IServiceProvider services, AgentExecution execution, CancellationToken ct)
    {
        var ctx = services.GetRequiredService<AppDbContext>();
        var aiFactory = services.GetRequiredService<AiProviderFactory>();

        // Get unclassified articles (up to 48h to catch backlog)
        var cutoff = DateTime.UtcNow.AddHours(-48);
        var unclassified = await ctx.NewsArticles
            .Include(a => a.Event)
            .Where(a => a.Event.EventType == "Unclassified" && a.CreatedAt >= cutoff)
            .OrderByDescending(a => a.CreatedAt)
            .Take(80)
            .ToListAsync(ct);

        if (unclassified.Count == 0)
        {
            await LogAsync(ctx, execution, AgentLogLevel.Info, "No hay articulos sin clasificar");
            return;
        }

        await LogAsync(ctx, execution, AgentLogLevel.Info, $"Procesando {unclassified.Count} articulos no clasificados");

        try
        {
            var aiProvider = await aiFactory.GetDefaultProviderAsync();
            var providerConfig = await ctx.AiProviderConfigs.FirstOrDefaultAsync(p => p.IsDefault && p.IsActive, ct);

            var clusters = await ClusterArticlesWithAiAsync(aiProvider, unclassified, execution, ctx, ct);

            foreach (var cluster in clusters)
            {
                await CreateOrUpdateEventAsync(ctx, cluster, execution, ct);
            }

            execution.ItemsProcessed = unclassified.Count;
            execution.ItemsCreated = clusters.Count;

            if (providerConfig != null)
            {
                execution.AiProviderConfigId = providerConfig.Id;
            }

            await LogAsync(ctx, execution, AgentLogLevel.Info, $"Detectados {clusters.Count} eventos de {unclassified.Count} articulos");
        }
        catch (Exception ex)
        {
            await LogAsync(ctx, execution, AgentLogLevel.Error, $"Error en deteccion de eventos: {ex.Message}");
            throw;
        }

        // Geocode events that have Location but no coordinates
        await GeocodeEventsMissingCoordsAsync(services, ctx, execution, ct);
    }

    private async Task GeocodeEventsMissingCoordsAsync(IServiceProvider services, AppDbContext ctx, AgentExecution execution, CancellationToken ct)
    {
        var missing = await ctx.NewsEvents
            .Where(e => e.Location != null && e.Latitude == null && e.IsActive)
            .Take(20)
            .ToListAsync(ct);

        if (missing.Count == 0) return;

        await LogAsync(ctx, execution, AgentLogLevel.Info, $"Geocodificando {missing.Count} eventos sin coordenadas");

        try
        {
            var httpFactory = services.GetRequiredService<IHttpClientFactory>();
            var http = httpFactory.CreateClient("nominatim");
            int geocoded = 0;

            foreach (var ev in missing)
            {
                if (ct.IsCancellationRequested) break;
                await Task.Delay(500, ct); // respect Nominatim rate limit
                try
                {
                    var url = $"search?q={Uri.EscapeDataString(ev.Location!)}&format=json&limit=1";
                    var res = await http.GetAsync(url, ct);
                    if (!res.IsSuccessStatusCode) continue;
                    var body = await res.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(body);
                    var arr = doc.RootElement;
                    if (arr.GetArrayLength() == 0) continue;
                    var first = arr[0];
                    ev.Latitude = double.Parse(first.GetProperty("lat").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                    ev.Longitude = double.Parse(first.GetProperty("lon").GetString()!, System.Globalization.CultureInfo.InvariantCulture);
                    geocoded++;
                }
                catch { /* skip individual failures */ }
            }

            if (geocoded > 0)
            {
                await ctx.SaveChangesAsync(ct);
                await LogAsync(ctx, execution, AgentLogLevel.Info, $"Geocodificados {geocoded} eventos en mapa");
            }
        }
        catch (Exception ex)
        {
            await LogAsync(ctx, execution, AgentLogLevel.Warning, $"Error en geocodificacion: {ex.Message}");
        }
    }

    private async Task<List<ArticleCluster>> ClusterArticlesWithAiAsync(
        IAiProvider ai, List<NewsArticle> articles, AgentExecution execution, AppDbContext ctx, CancellationToken ct)
    {
        var articlesList = articles.Select((a, i) => $"{i + 1}. [{a.SourceName}] {a.Title}").ToList();
        var articlesText = string.Join("\n", articlesList);

        var prompt = "Eres editor jefe de una agencia de noticias internacional. Agrupa estas noticias en EVENTOS DISTINTOS y clasificalos con precision periodistica.\n\n" +
            "REGLAS CRITICAS DE AGRUPACION:\n" +
            "1. Un evento = un hecho especifico en un lugar y tiempo concretos\n" +
            "2. NO mezcles eventos de paises distintos aunque sean del mismo tema\n" +
            "3. Agrupa solo si los articulos hablan EXACTAMENTE del mismo hecho\n" +
            "4. Un evento con un solo articulo es valido — no fuerces agrupaciones\n\n" +
            "UBICACION (CRITICO):\n" +
            "- Usa la ciudad o pais MAS ESPECIFICO posible\n" +
            "- Usa 'Gaza Strip' NO 'Middle East' si el epicentro es Gaza\n" +
            "- Usa 'Caracas' NO 'Venezuela' si la ciudad es el epicentro\n" +
            "- Usa regiones solo si el evento afecta multiples paises sin epicentro claro\n" +
            "- Coordenadas = EPICENTRO exacto del evento\n\n" +
            "PRIORIDAD: Critical=crisis activa global / High=impacto multinacional / Medium=alcance nacional / Low=local\n\n" +
            "IMPACT_SCORE 0-100: 90+=historico, 70-89=crisis nacional, 50-69=importante, 20-49=regional, 0-19=local\n\n" +
            "CATEGORIAS: Conflicto/Politica/Diplomacia/Economia/Tecnologia/Seguridad/Sociedad/Desastres/Justicia/Otros\n\n" +
            "Noticias:\n" + articlesText + "\n\n" +
            "Responde SOLO con JSON valido:\n" +
            "{\n  \"events\": [\n    {\n" +
            "      \"title\": \"Titulo descriptivo del evento\",\n" +
            "      \"description\": \"Descripcion 2-3 frases con contexto y relevancia\",\n" +
            "      \"priority\": \"High\",\n" +
            "      \"impact_score\": 75,\n" +
            "      \"category\": \"Politica\",\n" +
            "      \"location\": \"Madrid, Spain\",\n" +
            "      \"latitude\": 40.4168,\n" +
            "      \"longitude\": -3.7038,\n" +
            "      \"article_indices\": [1, 3, 5]\n" +
            "    }\n  ]\n}";

        var response = await ai.CompleteAsync(new AiRequest
        {
            SystemPrompt = "Eres un editor de noticias experto. Clasifica y agrupa noticias en eventos coherentes. Responde SIEMPRE con JSON valido.",
            UserPrompt = prompt,
            MaxTokens = 2000,
            Temperature = 0.2,
            OperationTag = "event_detection"
        }, ct);

        if (!response.Success) return [];

        // Track cost
        if (execution.AiProviderConfigId.HasValue)
        {
            ctx.CostEntries.Add(new CostEntry
            {
                AiProviderConfigId = execution.AiProviderConfigId.Value,
                AgentExecutionId = execution.Id,
                Operation = "event_detection",
                InputTokens = response.InputTokens,
                OutputTokens = response.OutputTokens,
                Cost = response.Cost,
                Date = DateTime.UtcNow
            });
            execution.AiCost += response.Cost;
        }

        try
        {
            var json = ExtractJson(response.Content);
            var result = JsonSerializer.Deserialize<EventDetectionResult>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            });

            return result?.Events?.Select(e => new ArticleCluster
            {
                Title = e.Title,
                Description = e.Description,
                Priority = Enum.TryParse<EventPriority>(e.Priority, true, out var p) ? p : EventPriority.Medium,
                ImpactScore = e.ImpactScore,
                Category = e.Category,
                Location = e.Location,
                Latitude = e.Latitude,
                Longitude = e.Longitude,
                Articles = e.ArticleIndices
                    .Where(i => i >= 1 && i <= articles.Count)
                    .Select(i => articles[i - 1])
                    .ToList()
            }).ToList() ?? [];
        }
        catch
        {
            await LogAsync(ctx, execution, AgentLogLevel.Warning, "No se pudo parsear respuesta de IA, usando clasificacion basica");
            return FallbackCluster(articles);
        }
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    private static List<ArticleCluster> FallbackCluster(List<NewsArticle> articles)
    {
        return [new ArticleCluster
        {
            Title = "Noticias del dia",
            Description = "Conjunto de noticias sin clasificar",
            Priority = EventPriority.Low,
            ImpactScore = 30,
            Category = "General",
            Articles = articles
        }];
    }

    private async Task CreateOrUpdateEventAsync(AppDbContext ctx, ArticleCluster cluster, AgentExecution execution, CancellationToken ct)
    {
        if (cluster.Articles.Count == 0) return;

        var sectionId = cluster.Articles.First().Event.NewsSectionId;

        // Try to find an existing recent event with a similar title to avoid duplicates
        var titleWords = cluster.Title.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cutoffDate = DateTime.UtcNow.AddDays(-3);
        var existingEvent = await ctx.NewsEvents
            .Where(e => e.EventType == "Detected" && e.IsActive && e.CreatedAt >= cutoffDate)
            .ToListAsync(ct);
        var matched = existingEvent.FirstOrDefault(e =>
        {
            var existingWords = e.Title.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var commonWords = titleWords.Intersect(existingWords).Count(w => w.Length > 4);
            return commonWords >= 2;
        });

        NewsEvent newsEvent;
        if (matched != null)
        {
            // Merge into existing event
            newsEvent = matched;
            if (cluster.Priority > newsEvent.Priority) newsEvent.Priority = cluster.Priority;
            if (cluster.ImpactScore > newsEvent.ImpactScore) newsEvent.ImpactScore = cluster.ImpactScore;
            newsEvent.Tags = newsEvent.Tags
                .Concat(cluster.Articles.SelectMany(a => a.Keywords))
                .Distinct().Take(15).ToArray();
        }
        else
        {
            newsEvent = new NewsEvent
            {
                Title = cluster.Title,
                Description = cluster.Description,
                Priority = cluster.Priority,
                ImpactScore = cluster.ImpactScore,
                Category = cluster.Category,
                EventType = "Detected",
                NewsSectionId = sectionId,
                StartDate = cluster.Articles.Min(a => a.PublishedAt),
                IsActive = true,
                Tags = cluster.Articles.SelectMany(a => a.Keywords).Distinct().Take(10).ToArray(),
                Location = cluster.Location,
                Latitude = cluster.Latitude,
                Longitude = cluster.Longitude
            };
            ctx.NewsEvents.Add(newsEvent);
            await ctx.SaveChangesAsync(ct);
        }

        // Reassign articles to this event
        foreach (var article in cluster.Articles)
        {
            article.NewsEventId = newsEvent.Id;
        }

        await ctx.SaveChangesAsync(ct);
    }
}

record ArticleCluster
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public EventPriority Priority { get; init; }
    public decimal ImpactScore { get; init; }
    public required string Category { get; init; }
    public string? Location { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public required List<NewsArticle> Articles { get; init; }
}

record EventDetectionResult
{
    public List<EventDto>? Events { get; init; }
}

record EventDto
{
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public string Priority { get; init; } = "Medium";
    [System.Text.Json.Serialization.JsonPropertyName("impact_score")]
    public decimal ImpactScore { get; init; } = 50;
    public string Category { get; init; } = "General";
    public string? Location { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    [System.Text.Json.Serialization.JsonPropertyName("article_indices")]
    public List<int> ArticleIndices { get; init; } = [];
}
