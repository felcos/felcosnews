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

/// <summary>
/// Agente que revisa articulos y eventos con contenido en otros idiomas
/// y los traduce al español. Mantiene el contenido original accesible
/// via el campo Language del articulo para el toggle ES/EN de la UI.
/// </summary>
public class TranslatorAgent : BaseAgent
{
    protected override AgentType AgentType => AgentType.Translator;
    protected override string AgentName => "TranslatorAgent";
    protected override TimeSpan DefaultInterval => TimeSpan.FromHours(1);

    public TranslatorAgent(IServiceProvider services, ILogger<TranslatorAgent> logger)
        : base(services, logger) { }

    protected override async Task RunCycleAsync(IServiceProvider services, AgentExecution execution, CancellationToken ct)
    {
        AppDbContext ctx = services.GetRequiredService<AppDbContext>();
        AiProviderFactory aiFactory = services.GetRequiredService<AiProviderFactory>();

        IAiProvider aiProvider;
        try { aiProvider = await aiFactory.GetDefaultProviderAsync(); }
        catch (Exception ex)
        {
            await LogAsync(ctx, execution, AgentLogLevel.Error, $"Sin proveedor IA: {ex.Message}");
            return;
        }

        AiProviderConfig? providerConfig = await ctx.AiProviderConfigs
            .FirstOrDefaultAsync(p => p.IsDefault && p.IsActive, ct);
        if (providerConfig != null) execution.AiProviderConfigId = providerConfig.Id;

        // Fase 1: Traducir articulos con titulo/resumen en otros idiomas
        int articles = await TranslateArticlesAsync(ctx, aiProvider, execution, ct);

        // Fase 2: Traducir eventos con titulo/descripcion en otros idiomas
        int events = await TranslateEventsAsync(ctx, aiProvider, execution, ct);

        execution.ItemsProcessed = articles + events;
        execution.ItemsCreated = articles + events;
        await LogAsync(ctx, execution, AgentLogLevel.Info,
            $"Traducidos: {articles} articulos, {events} eventos");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Fase 1: Traducir articulos no-español
    // ─────────────────────────────────────────────────────────────────────────────
    private async Task<int> TranslateArticlesAsync(
        AppDbContext ctx, IAiProvider ai, AgentExecution execution, CancellationToken ct)
    {
        // Buscar articulos que:
        // - Ya fueron procesados por el Summarizer (tienen keywords)
        // - Tienen idioma != "es"
        // - Fueron creados en las ultimas 72h (no reprocesar historico infinito)
        // - Su titulo parece no estar en español (heuristica + language field)
        DateTime cutoff = DateTime.UtcNow.AddHours(-72);
        List<NewsArticle> pending = await ctx.NewsArticles
            .Where(a => !a.IsDeleted
                     && a.CreatedAt >= cutoff
                     && a.Keywords.Any()          // ya procesado por summarizer
                     && a.Language != "es")        // idioma no español
            .OrderByDescending(a => a.CreatedAt)
            .Take(30)
            .ToListAsync(ct);

        if (pending.Count == 0) return 0;

        await LogAsync(ctx, execution, AgentLogLevel.Info,
            $"Traduciendo {pending.Count} articulos de idioma no-español");

        // Procesar en batches de 10
        int translated = 0;
        foreach (NewsArticle[] batch in pending.Chunk(10))
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                int batchTranslated = await TranslateArticleBatchAsync(ctx, ai, execution, batch, ct);
                translated += batchTranslated;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TranslatorAgent] Error traduciendo batch de articulos");
                await LogAsync(ctx, execution, AgentLogLevel.Warning, $"Error en batch de articulos: {ex.Message}");
            }
        }

        return translated;
    }

    private async Task<int> TranslateArticleBatchAsync(
        AppDbContext ctx, IAiProvider ai, AgentExecution execution,
        NewsArticle[] articles, CancellationToken ct)
    {
        string articlesJson = string.Join("\n", articles.Select((a, i) =>
        {
            string summary = Truncate(a.Summary ?? "", 300);
            return $"{i + 1}. TITULO: {a.Title}\n   RESUMEN: {summary}\n   IDIOMA: {a.Language}";
        }));

        string prompt =
            "Traduce al ESPAÑOL los titulos y resumenes de estos articulos.\n" +
            "REGLAS:\n" +
            "- Traduccion precisa y natural, no literal\n" +
            "- Mantener nombres propios sin traducir (personas, empresas, lugares)\n" +
            "- No añadir ni quitar informacion\n" +
            "- Si el titulo ya esta en español correcto, dejarlo igual\n" +
            "- Limpiar caracteres raros, HTML entities o codificaciones rotas\n\n" +
            "ARTICULOS:\n" + articlesJson + "\n\n" +
            "Responde SOLO con JSON valido:\n" +
            "{\"translations\": [{\"index\": 1, \"title_es\": \"titulo en español\", \"summary_es\": \"resumen en español\"}]}";

        AiResponse response = await ai.CompleteAsync(new AiRequest
        {
            SystemPrompt = "Eres un traductor profesional de noticias. Traduce al español con precision periodistica. " +
                           "Responde SOLO con JSON valido.",
            UserPrompt = prompt,
            MaxTokens = 2000,
            Temperature = 0.2,
            OperationTag = "article_translation"
        }, ct);

        if (!response.Success) return 0;
        TrackCostFromResponse(ctx, execution, response);

        try
        {
            string json = ExtractJson(response.Content);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("translations", out JsonElement translations)) return 0;

            int count = 0;
            foreach (JsonElement item in translations.EnumerateArray())
            {
                if (!item.TryGetProperty("index", out JsonElement idxEl)) continue;
                int idx = idxEl.GetInt32() - 1;
                if (idx < 0 || idx >= articles.Length) continue;

                NewsArticle article = articles[idx];

                if (item.TryGetProperty("title_es", out JsonElement titleEs))
                {
                    string? newTitle = titleEs.GetString();
                    if (!string.IsNullOrWhiteSpace(newTitle))
                        article.Title = CleanText(newTitle);
                }

                if (item.TryGetProperty("summary_es", out JsonElement summaryEs))
                {
                    string? newSummary = summaryEs.GetString();
                    if (!string.IsNullOrWhiteSpace(newSummary))
                        article.Summary = CleanText(newSummary);
                }

                // Marcar como traducido: el campo Language pasa a "es"
                // para que no se vuelva a procesar
                article.Language = "es";
                count++;
            }

            await ctx.SaveChangesAsync(ct);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TranslatorAgent] Error parseando traducciones de articulos");
            return 0;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Fase 2: Traducir eventos con contenido no-español
    // ─────────────────────────────────────────────────────────────────────────────
    private async Task<int> TranslateEventsAsync(
        AppDbContext ctx, IAiProvider ai, AgentExecution execution, CancellationToken ct)
    {
        // Detectar eventos cuyo titulo contiene palabras tipicamente no-español
        // La heuristica: si la mayoria de articulos del evento son no-español,
        // o si el titulo tiene patron de ingles (the, and, of, for, with, etc.)
        DateTime cutoff = DateTime.UtcNow.AddHours(-72);
        List<NewsEvent> candidates = await ctx.NewsEvents
            .Where(e => !e.IsDeleted
                     && e.IsActive
                     && e.EventType == "Detected"
                     && e.CreatedAt >= cutoff)
            .OrderByDescending(e => e.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        // Filtrar los que parecen no estar en español
        List<NewsEvent> needsTranslation = candidates
            .Where(e => LooksNonSpanish(e.Title) || LooksNonSpanish(e.Description))
            .ToList();

        if (needsTranslation.Count == 0) return 0;

        await LogAsync(ctx, execution, AgentLogLevel.Info,
            $"Detectados {needsTranslation.Count} eventos con posible contenido no-español");

        // Procesar en batches de 10
        int translated = 0;
        foreach (NewsEvent[] batch in needsTranslation.Chunk(10))
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                int batchTranslated = await TranslateEventBatchAsync(ctx, ai, execution, batch, ct);
                translated += batchTranslated;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[TranslatorAgent] Error traduciendo batch de eventos");
                await LogAsync(ctx, execution, AgentLogLevel.Warning, $"Error en batch de eventos: {ex.Message}");
            }
        }

        return translated;
    }

    private async Task<int> TranslateEventBatchAsync(
        AppDbContext ctx, IAiProvider ai, AgentExecution execution,
        NewsEvent[] events, CancellationToken ct)
    {
        string eventsJson = string.Join("\n", events.Select((e, i) =>
            $"{i + 1}. TITULO: {e.Title}\n   DESCRIPCION: {Truncate(e.Description ?? "", 300)}"));

        string prompt =
            "Analiza estos titulos y descripciones de eventos noticiosos.\n" +
            "Para cada uno:\n" +
            "1. Detecta si esta en español o en otro idioma\n" +
            "2. Si NO esta completamente en español, traducelo al español\n" +
            "3. Si YA esta en español correcto, devuelvelo igual (is_spanish: true)\n\n" +
            "REGLAS:\n" +
            "- Traduccion natural y periodistica\n" +
            "- Nombres propios sin traducir\n" +
            "- No añadir informacion que no este en el original\n" +
            "- Limpiar caracteres extraños o HTML\n\n" +
            "EVENTOS:\n" + eventsJson + "\n\n" +
            "Responde SOLO con JSON valido:\n" +
            "{\"translations\": [{\"index\": 1, \"is_spanish\": false, \"title_es\": \"titulo traducido\", \"description_es\": \"descripcion traducida\"}]}";

        AiResponse response = await ai.CompleteAsync(new AiRequest
        {
            SystemPrompt = "Eres un traductor profesional de noticias internacionales. Detecta el idioma y traduce al español si es necesario. " +
                           "Responde SOLO con JSON valido.",
            UserPrompt = prompt,
            MaxTokens = 2000,
            Temperature = 0.2,
            OperationTag = "event_translation"
        }, ct);

        if (!response.Success) return 0;
        TrackCostFromResponse(ctx, execution, response);

        try
        {
            string json = ExtractJson(response.Content);
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (!root.TryGetProperty("translations", out JsonElement translations)) return 0;

            int count = 0;
            foreach (JsonElement item in translations.EnumerateArray())
            {
                if (!item.TryGetProperty("index", out JsonElement idxEl)) continue;
                int idx = idxEl.GetInt32() - 1;
                if (idx < 0 || idx >= events.Length) continue;

                // Si la IA confirma que ya esta en español, skip
                if (item.TryGetProperty("is_spanish", out JsonElement isEs) && isEs.GetBoolean())
                    continue;

                NewsEvent ev = events[idx];

                if (item.TryGetProperty("title_es", out JsonElement titleEs))
                {
                    string? newTitle = titleEs.GetString();
                    if (!string.IsNullOrWhiteSpace(newTitle))
                        ev.Title = CleanText(newTitle);
                }

                if (item.TryGetProperty("description_es", out JsonElement descEs))
                {
                    string? newDesc = descEs.GetString();
                    if (!string.IsNullOrWhiteSpace(newDesc))
                        ev.Description = CleanText(newDesc);
                }

                count++;
            }

            await ctx.SaveChangesAsync(ct);
            return count;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TranslatorAgent] Error parseando traducciones de eventos");
            return 0;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Heuristica: detectar si un texto probablemente NO esta en español
    // ─────────────────────────────────────────────────────────────────────────────
    private static bool LooksNonSpanish(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        string lower = text.ToLowerInvariant();
        string[] words = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 3) return false;

        // Palabras muy comunes en ingles que rara vez aparecen en texto español
        string[] englishIndicators = [
            "the", "and", "for", "with", "from", "that", "this", "has", "have",
            "was", "were", "been", "will", "would", "could", "should", "after",
            "before", "about", "into", "over", "between", "through", "during",
            "says", "said", "according", "amid", "announces", "launches",
            "report", "officials", "government", "president", "minister"
        ];

        // Palabras comunes en frances
        string[] frenchIndicators = [
            "les", "des", "une", "pour", "dans", "avec", "sont", "sur", "qui",
            "est", "par", "aux", "cette", "selon", "mais", "lors", "entre"
        ];

        // Palabras comunes en aleman
        string[] germanIndicators = [
            "der", "die", "das", "und", "ist", "von", "mit", "auf", "fur",
            "ein", "eine", "nicht", "sich", "nach", "werden", "auch"
        ];

        // Palabras comunes en portugues (que no son español)
        string[] portugueseIndicators = [
            "com", "uma", "para", "foi", "são", "tem", "mais", "pelo",
            "pela", "isso", "pode", "mas", "muito", "também", "após"
        ];

        int englishCount = words.Count(w => englishIndicators.Contains(w));
        int frenchCount = words.Count(w => frenchIndicators.Contains(w));
        int germanCount = words.Count(w => germanIndicators.Contains(w));
        int portugueseCount = words.Count(w => portugueseIndicators.Contains(w));
        int foreignCount = englishCount + frenchCount + germanCount + portugueseCount;

        // Si >= 20% de las palabras son indicadores extranjeros, probablemente no es español
        double ratio = (double)foreignCount / words.Length;
        return ratio >= 0.2 || englishCount >= 3 || frenchCount >= 3 || germanCount >= 3;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────────
    private void TrackCostFromResponse(AppDbContext ctx, AgentExecution execution, AiResponse response)
    {
        if (!execution.AiProviderConfigId.HasValue || response.Cost <= 0) return;
        ctx.CostEntries.Add(new CostEntry
        {
            AiProviderConfigId = execution.AiProviderConfigId.Value,
            AgentExecutionId = execution.Id,
            Operation = "translation",
            InputTokens = response.InputTokens,
            OutputTokens = response.OutputTokens,
            Cost = response.Cost,
            Date = DateTime.UtcNow
        });
        execution.AiCost += response.Cost;
    }

    private static string ExtractJson(string text)
    {
        int start = text.IndexOf('{');
        int end = text.LastIndexOf('}');
        return start >= 0 && end > start ? text[start..(end + 1)] : text;
    }

    private static string Truncate(string s, int max) => s.Length > max ? s[..max] : s;

    private static string CleanText(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"[\u200B-\u200F\u202A-\u202E\uFEFF\u00AD]", "");
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        return text;
    }
}
