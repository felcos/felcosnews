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

public class ArticleSummarizerAgent : BaseAgent
{
    protected override AgentType AgentType => AgentType.ArticleSummarizer;
    protected override string AgentName => "ArticleSummarizerAgent";
    protected override TimeSpan Interval => TimeSpan.FromMinutes(45);

    public ArticleSummarizerAgent(IServiceProvider services, ILogger<ArticleSummarizerAgent> logger)
        : base(services, logger) { }

    protected override async Task RunCycleAsync(IServiceProvider services, AgentExecution execution, CancellationToken ct)
    {
        var ctx = services.GetRequiredService<AppDbContext>();
        var aiFactory = services.GetRequiredService<AiProviderFactory>();

        // Articles without AI processing: Keywords is empty and created in last 48h
        var cutoff = DateTime.UtcNow.AddHours(-48);
        // Use !Any() instead of Length == 0 — EF Core translates this to jsonb_array_length > 0
        // which works correctly on PostgreSQL JSONB columns (cardinality() does not work on JSONB)
        var pending = await ctx.NewsArticles
            .Where(a => !a.IsDeleted && !a.Keywords.Any() && a.CreatedAt >= cutoff)
            .OrderByDescending(a => a.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        if (pending.Count == 0)
        {
            await LogAsync(ctx, execution, AgentLogLevel.Info, "No hay artículos pendientes de resumen");
            return;
        }

        await LogAsync(ctx, execution, AgentLogLevel.Info, $"Resumiendo {pending.Count} artículos con IA");

        IAiProvider aiProvider;
        try
        {
            aiProvider = await aiFactory.GetDefaultProviderAsync();
        }
        catch
        {
            await LogAsync(ctx, execution, AgentLogLevel.Warning, "Sin proveedor IA activo — resumiendo con extracto RSS");
            // Fallback: mark as processed with empty keywords placeholder to avoid re-processing
            foreach (var art in pending)
                art.Keywords = ["rss"];
            await ctx.SaveChangesAsync(ct);
            execution.ItemsProcessed = pending.Count;
            return;
        }

        // Process in batches of 10
        var batches = pending.Chunk(10);
        int summarized = 0;

        foreach (var batch in batches)
        {
            try
            {
                var results = await SummarizeBatchAsync(aiProvider, batch, ct);
                foreach (var (article, idx) in batch.Select((a, i) => (a, i)))
                {
                    if (results.TryGetValue(idx, out var result))
                    {
                        if (!string.IsNullOrWhiteSpace(result.Summary))
                            article.Summary = result.Summary;
                        article.Keywords = result.Keywords.Length > 0 ? result.Keywords : ["ai"];
                    }
                    else
                    {
                        article.Keywords = ["ai"]; // Mark as processed even if AI failed for this item
                    }
                    summarized++;
                }
                await ctx.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error resumiendo batch de artículos");
                await LogAsync(ctx, execution, AgentLogLevel.Warning, $"Error en batch: {ex.Message}");
            }
        }

        execution.ItemsProcessed = pending.Count;
        execution.ItemsCreated = summarized;
        await LogAsync(ctx, execution, AgentLogLevel.Info, $"Resumidos {summarized} artículos");
    }

    private async Task<Dictionary<int, SummaryResult>> SummarizeBatchAsync(
        IAiProvider ai, IEnumerable<ANews.Domain.Entities.NewsArticle> articles, CancellationToken ct)
    {
        var list = articles.ToList();
        var articlesJson = list.Select((a, i) => new
        {
            index = i,
            title = a.Title,
            text = (a.Summary ?? "")[..Math.Min(a.Summary?.Length ?? 0, 500)]
        });

        var responseFormat = "{\"summaries\":[{\"index\":0,\"summary\":\"resumen aquí\",\"keywords\":[\"kw1\",\"kw2\",\"kw3\"]}]}";
        var prompt = $"""
            Eres un analista de inteligencia geopolítica. Para cada artículo, genera un resumen conciso de 2-3 frases en español y extrae 3-5 palabras clave relevantes.

            Artículos:
            {JsonSerializer.Serialize(articlesJson)}

            Responde SOLO con JSON válido, sin texto adicional, en este formato:
            {responseFormat}
            """;

        var request = new AiRequest
        {
            SystemPrompt = "Analista de inteligencia. Responde SOLO con JSON válido.",
            UserPrompt = prompt,
            MaxTokens = 1500,
            Temperature = 0.3
        };

        var response = await ai.CompleteAsync(request, ct);
        return ParseSummaries(response.Content ?? "");
    }

    private static Dictionary<int, SummaryResult> ParseSummaries(string json)
    {
        var result = new Dictionary<int, SummaryResult>();
        try
        {
            var start = json.IndexOf('{');
            var end = json.LastIndexOf('}');
            if (start < 0 || end < 0) return result;
            json = json[start..(end + 1)];

            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            };
            var parsed = JsonSerializer.Deserialize<SummaryResponse>(json, opts);
            if (parsed?.Summaries == null) return result;

            foreach (var s in parsed.Summaries)
                result[s.Index] = new SummaryResult(s.Summary ?? "", s.Keywords ?? []);
        }
        catch { /* return empty */ }
        return result;
    }

    record SummaryResponse
    {
        public List<SummaryItem>? Summaries { get; init; }
    }

    record SummaryItem
    {
        public int Index { get; init; }
        public string? Summary { get; init; }
        public string[]? Keywords { get; init; }
    }

    record SummaryResult(string Summary, string[] Keywords);
}
