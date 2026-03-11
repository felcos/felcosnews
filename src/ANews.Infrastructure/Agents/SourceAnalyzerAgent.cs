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

public class SourceAnalyzerAgent : BaseAgent
{
    protected override AgentType AgentType => AgentType.SourceAnalyzer;
    protected override string AgentName => "SourceAnalyzerAgent";
    protected override TimeSpan DefaultInterval => TimeSpan.FromHours(12);

    public SourceAnalyzerAgent(IServiceProvider services, ILogger<SourceAnalyzerAgent> logger)
        : base(services, logger) { }

    protected override async Task RunCycleAsync(IServiceProvider services, AgentExecution execution, CancellationToken ct)
    {
        var ctx = services.GetRequiredService<AppDbContext>();
        var aiFactory = services.GetRequiredService<AiProviderFactory>();

        IAiProvider aiProvider;
        try { aiProvider = await aiFactory.GetDefaultProviderAsync(); }
        catch (Exception ex)
        {
            await LogAsync(ctx, execution, AgentLogLevel.Error, $"Sin proveedor IA: {ex.Message}");
            return;
        }

        var providerConfig = await ctx.AiProviderConfigs.FirstOrDefaultAsync(p => p.IsDefault && p.IsActive, ct);
        if (providerConfig != null) execution.AiProviderConfigId = providerConfig.Id;

        // Phase 1: Update speed scores based on article timing
        int speedUpdated = await UpdateSpeedScoresAsync(ctx, ct);

        // Phase 2: Update credibility based on article quality metrics
        int credibilityUpdated = await UpdateCredibilityScoresAsync(ctx, ct);

        // Phase 3: Analyze bias for sources that haven't been analyzed yet (AI)
        int biasAnalyzed = await AnalyzeBiasAsync(ctx, aiProvider, execution, ct);

        execution.ItemsProcessed = speedUpdated + credibilityUpdated + biasAnalyzed;
        await LogAsync(ctx, execution, AgentLogLevel.Info,
            $"Velocidad: {speedUpdated} fuentes, Credibilidad: {credibilityUpdated} fuentes, Sesgo: {biasAnalyzed} fuentes analizadas");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Phase 1: Speed scores — how fast does a source report vs others?
    // ─────────────────────────────────────────────────────────────────────────────
    private async Task<int> UpdateSpeedScoresAsync(AppDbContext ctx, CancellationToken ct)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var sources = await ctx.NewsSources
            .Where(s => s.IsActive && !s.IsDeleted && s.TotalArticlesFound > 10)
            .ToListAsync(ct);

        if (sources.Count == 0) return 0;

        // For each source, calculate how often it's the first to report on an event
        foreach (var source in sources)
        {
            var sourceArticles = await ctx.NewsArticles
                .Where(a => a.NewsSourceId == source.Id && a.PublishedAt >= cutoff && !a.IsDeleted)
                .Select(a => new { a.NewsEventId, a.PublishedAt })
                .ToListAsync(ct);

            if (sourceArticles.Count < 3) continue;

            int firstCount = 0;
            foreach (var article in sourceArticles)
            {
                var earliestForEvent = await ctx.NewsArticles
                    .Where(a => a.NewsEventId == article.NewsEventId && !a.IsDeleted)
                    .MinAsync(a => (DateTime?)a.PublishedAt, ct);

                if (earliestForEvent.HasValue &&
                    Math.Abs((article.PublishedAt - earliestForEvent.Value).TotalMinutes) < 30)
                    firstCount++;
            }

            // Speed score: 0-100, based on % of events where this source was among the first
            source.SpeedScore = sourceArticles.Count > 0
                ? Math.Min(100, (int)(firstCount * 100.0 / sourceArticles.Count))
                : 50;
        }

        await ctx.SaveChangesAsync(ct);
        return sources.Count;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Phase 2: Credibility — based on article density, diversity, reliability
    // ─────────────────────────────────────────────────────────────────────────────
    private async Task<int> UpdateCredibilityScoresAsync(AppDbContext ctx, CancellationToken ct)
    {
        var sources = await ctx.NewsSources
            .Where(s => s.IsActive && !s.IsDeleted)
            .ToListAsync(ct);

        foreach (var source in sources)
        {
            // Credibility formula:
            // Base 50 + reliability bonus + volume bonus - failure penalty
            var reliability = source.SuccessfulScans > 0
                ? (double)source.SuccessfulScans / (source.SuccessfulScans + source.FailedScans) * 30
                : 15;

            var volumeBonus = Math.Min(20, source.TotalArticlesFound / 50.0);
            var failurePenalty = Math.Min(30, source.FailedScans * 2);
            var correctionPenalty = Math.Min(10, source.CorrectionCount * 3);

            source.CredibilityScore = (int)Math.Clamp(50 + reliability + volumeBonus - failurePenalty - correctionPenalty, 10, 100);
        }

        await ctx.SaveChangesAsync(ct);
        return sources.Count;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Phase 3: Bias analysis via AI — analyze recent articles from the source
    // ─────────────────────────────────────────────────────────────────────────────
    private async Task<int> AnalyzeBiasAsync(
        AppDbContext ctx, IAiProvider ai, AgentExecution execution, CancellationToken ct)
    {
        // Find sources with Unknown bias that have enough articles
        var sources = await ctx.NewsSources
            .Where(s => s.IsActive && !s.IsDeleted
                     && s.Bias == BiasIndicator.Unknown
                     && s.TotalArticlesFound >= 20)
            .OrderByDescending(s => s.TotalArticlesFound)
            .Take(10)
            .ToListAsync(ct);

        if (sources.Count == 0) return 0;

        int analyzed = 0;
        foreach (var source in sources)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                // Get recent article titles/summaries from this source
                var recentArticles = await ctx.NewsArticles
                    .Where(a => a.NewsSourceId == source.Id && !a.IsDeleted)
                    .OrderByDescending(a => a.PublishedAt)
                    .Take(20)
                    .Select(a => new { a.Title, Summary = a.Summary ?? "" })
                    .ToListAsync(ct);

                if (recentArticles.Count < 10) continue;

                var articleSample = string.Join("\n", recentArticles.Select((a, i) =>
                    $"{i + 1}. {a.Title}" + (a.Summary.Length > 0 ? $" — {a.Summary[..Math.Min(a.Summary.Length, 100)]}" : "")));

                var prompt =
                    $"Fuente de noticias: {source.Name}\n" +
                    $"URL: {source.Url}\n" +
                    $"Idioma: {source.Language}\n\n" +
                    $"MUESTRA DE ARTÍCULOS RECIENTES:\n{articleSample}\n\n" +
                    "Analiza la orientación editorial de esta fuente basándote en:\n" +
                    "1. Selección de temas (qué cubre y qué ignora)\n" +
                    "2. Lenguaje y framing (cómo presenta los hechos)\n" +
                    "3. Fuentes citadas y perspectivas representadas\n\n" +
                    "Clasifica el sesgo editorial:\n" +
                    "- Left: progresista/izquierda clara\n" +
                    "- CenterLeft: centro-izquierda moderado\n" +
                    "- Center: equilibrado/neutral\n" +
                    "- CenterRight: centro-derecha moderado\n" +
                    "- Right: conservador/derecha clara\n" +
                    "- State: medio estatal/gubernamental\n\n" +
                    "Analiza también la densidad de hechos (0-100): ¿cuántos hechos verificables vs opiniones?\n\n" +
                    "Responde SOLO con JSON:\n" +
                    "{\"bias\": \"Center\", \"fact_density\": 72, \"reasoning\": \"breve explicación en 1 frase\"}";

                var response = await ai.CompleteAsync(new AiRequest
                {
                    SystemPrompt = "Eres un analista de medios objetivo. Clasifica sesgo editorial sin juicio de valor. Responde SOLO con JSON válido.",
                    UserPrompt = prompt,
                    MaxTokens = 300,
                    Temperature = 0.2,
                    OperationTag = "source_analysis"
                }, ct);

                if (!response.Success) continue;
                TrackCostFromResponse(ctx, execution, response);

                var json = ExtractJson(response.Content);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var biasStr = root.TryGetProperty("bias", out var b) ? b.GetString() : null;
                if (Enum.TryParse<BiasIndicator>(biasStr, true, out var bias))
                    source.Bias = bias;

                if (root.TryGetProperty("fact_density", out var fd))
                    source.FactDensityAvg = fd.GetDecimal();

                await ctx.SaveChangesAsync(ct);
                analyzed++;

                await LogAsync(ctx, execution, AgentLogLevel.Info,
                    $"[{source.Name}] Sesgo: {source.Bias}, Fact density: {source.FactDensityAvg:F0}");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[SourceAnalyzer] Error analyzing {Source}", source.Name);
            }
        }

        return analyzed;
    }

    private void TrackCostFromResponse(AppDbContext ctx, AgentExecution execution, AiResponse response)
    {
        if (!execution.AiProviderConfigId.HasValue || response.Cost <= 0) return;
        ctx.CostEntries.Add(new CostEntry
        {
            AiProviderConfigId = execution.AiProviderConfigId.Value,
            AgentExecutionId = execution.Id,
            Operation = "source_analysis",
            InputTokens = response.InputTokens,
            OutputTokens = response.OutputTokens,
            Cost = response.Cost,
            Date = DateTime.UtcNow
        });
        execution.AiCost += response.Cost;
    }

    private static string ExtractJson(string text)
    {
        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }
}
