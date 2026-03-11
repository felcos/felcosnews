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

public class BriefingGeneratorAgent : BaseAgent
{
    protected override AgentType AgentType => AgentType.BriefingGenerator;
    protected override string AgentName => "BriefingGeneratorAgent";
    protected override TimeSpan DefaultInterval => TimeSpan.FromHours(4);

    public BriefingGeneratorAgent(IServiceProvider services, ILogger<BriefingGeneratorAgent> logger)
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

        // Phase 1: Generate contextual briefings for high-impact events without one
        int briefingsCreated = await GenerateEventBriefingsAsync(ctx, aiProvider, execution, ct);

        // Phase 2: Generate morning brief if none exists for today
        bool morningBriefCreated = await GenerateMorningBriefAsync(ctx, aiProvider, execution, ct);

        // Phase 3: Generate thread summaries for active story threads
        int threadSummaries = await UpdateStoryThreadSummariesAsync(ctx, aiProvider, execution, ct);

        execution.ItemsProcessed = briefingsCreated + (morningBriefCreated ? 1 : 0) + threadSummaries;
        execution.ItemsCreated = briefingsCreated;
        await LogAsync(ctx, execution, AgentLogLevel.Info,
            $"Generados: {briefingsCreated} briefings de evento, " +
            $"{(morningBriefCreated ? "1 morning brief" : "morning brief ya existía")}, " +
            $"{threadSummaries} resúmenes de hilo narrativo");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Phase 1: Event contextual briefings
    // ─────────────────────────────────────────────────────────────────────────────
    private async Task<int> GenerateEventBriefingsAsync(
        AppDbContext ctx, IAiProvider ai, AgentExecution execution, CancellationToken ct)
    {
        // Find high-impact events (>40) without briefing, last 3 days
        var cutoff = DateTime.UtcNow.AddDays(-3);
        var eventIds = await ctx.EventBriefings
            .Where(b => b.Type == BriefingType.EventContext)
            .Select(b => b.NewsEventId)
            .ToListAsync(ct);

        var events = await ctx.NewsEvents
            .Include(e => e.Section)
            .Include(e => e.Articles.Where(a => !a.IsDeleted))
            .Where(e => e.IsActive
                     && e.ImpactScore >= 40
                     && e.EventType == "Detected"
                     && e.CreatedAt >= cutoff
                     && !eventIds.Contains(e.Id))
            .OrderByDescending(e => e.ImpactScore)
            .Take(15)
            .ToListAsync(ct);

        if (events.Count == 0) return 0;

        await LogAsync(ctx, execution, AgentLogLevel.Info,
            $"Generando briefings contextuales para {events.Count} eventos");

        int created = 0;
        foreach (var ev in events)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var articles = ev.Articles.OrderByDescending(a => a.Relevance).Take(10).ToList();
                var articleContext = string.Join("\n", articles.Select(a =>
                    $"- [{a.SourceName}] {a.Title}: {Truncate(a.Summary ?? a.Content ?? "", 200)}"));

                var prompt =
                    $"Evento: {ev.Title}\n" +
                    $"Sección: {ev.Section?.Name}\n" +
                    $"Descripción: {ev.Description}\n" +
                    $"Ubicación: {ev.Location}\n" +
                    $"Fuentes ({articles.Count}):\n{articleContext}\n\n" +
                    "Genera un BRIEFING CONTEXTUAL para un lector que necesita entender este evento rápidamente.\n" +
                    "El briefing debe responder las preguntas fundamentales del periodismo.\n\n" +
                    "Responde en JSON:\n" +
                    "{\"why_it_matters\": \"Por qué este evento es importante y cómo afecta (2-3 frases)\",\n" +
                    " \"background\": \"Antecedentes y contexto histórico que explican este evento (2-3 frases)\",\n" +
                    " \"key_actors\": \"Actores principales involucrados y su papel (2-3 frases)\",\n" +
                    " \"what_to_watch\": \"Qué vigilar, posibles consecuencias y evolución probable (2-3 frases)\"}";

                var response = await ai.CompleteAsync(new AiRequest
                {
                    SystemPrompt = "Eres un analista de inteligencia informativa. Genera briefings contextuales concisos, objetivos y útiles en español. Responde SOLO con JSON válido.",
                    UserPrompt = prompt,
                    MaxTokens = 800,
                    Temperature = 0.3,
                    OperationTag = "event_briefing"
                }, ct);

                if (!response.Success) continue;
                TrackCostFromResponse(ctx, execution, response);

                var json = ExtractJson(response.Content);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var briefing = new EventBriefing
                {
                    NewsEventId = ev.Id,
                    Type = BriefingType.EventContext,
                    Title = ev.Title,
                    WhyItMatters = root.TryGetProperty("why_it_matters", out var wim) ? wim.GetString() : null,
                    Background = root.TryGetProperty("background", out var bg) ? bg.GetString() : null,
                    KeyActors = root.TryGetProperty("key_actors", out var ka) ? ka.GetString() : null,
                    WhatToWatch = root.TryGetProperty("what_to_watch", out var wtw) ? wtw.GetString() : null,
                    GeneratedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddDays(3),
                    SourceArticleCount = articles.Count
                };
                ctx.EventBriefings.Add(briefing);
                await ctx.SaveChangesAsync(ct);
                created++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[BriefingGenerator] Error generating briefing for event {EventId}", ev.Id);
            }
        }

        return created;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Phase 2: Morning brief
    // ─────────────────────────────────────────────────────────────────────────────
    private async Task<bool> GenerateMorningBriefAsync(
        AppDbContext ctx, IAiProvider ai, AgentExecution execution, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var existing = await ctx.MorningBriefs.AnyAsync(m => m.BriefDate == today, ct);
        if (existing) return false;

        // Gather top events from last 24h
        var since = DateTime.UtcNow.AddHours(-24);
        var topEvents = await ctx.NewsEvents
            .Include(e => e.Section)
            .Where(e => e.IsActive && e.EventType == "Detected" && e.CreatedAt >= since)
            .OrderByDescending(e => e.ImpactScore)
            .Take(30)
            .ToListAsync(ct);

        if (topEvents.Count < 3)
        {
            await LogAsync(ctx, execution, AgentLogLevel.Info, "Insuficientes eventos para morning brief");
            return false;
        }

        // Load active developing threads
        var devThreads = await ctx.StoryThreads
            .Where(t => t.Status == StoryStatus.Developing || t.Status == StoryStatus.Active)
            .OrderByDescending(t => t.MaxImpactScore)
            .Take(5)
            .ToListAsync(ct);

        var eventList = string.Join("\n", topEvents.Select((e, i) =>
            $"{i + 1}. [{e.Section?.Name}] {e.Title} | Impacto:{e.ImpactScore:F0} | {e.Description?[..Math.Min(e.Description?.Length ?? 0, 100)]}"));

        var threadList = devThreads.Count > 0
            ? string.Join("\n", devThreads.Select(t => $"- {t.Title} ({t.EventCount} eventos, status: {t.Status})"))
            : "(sin hilos activos)";

        var prompt =
            "Eres el editor jefe de AgenteNews. Prepara el MORNING BRIEF del día.\n" +
            "Este briefing es lo primero que lee el usuario. Debe ser editorial, conciso y útil.\n\n" +
            "EVENTOS DE LAS ÚLTIMAS 24H:\n" + eventList + "\n\n" +
            "HILOS NARRATIVOS EN DESARROLLO:\n" + threadList + "\n\n" +
            "Genera un morning brief con esta estructura:\n" +
            "1. TITULAR del día: una frase que resuma el estado del mundo hoy\n" +
            "2. TOP 3 HISTORIAS: las 3 cosas que debes saber hoy (breve, 2-3 frases cada una)\n" +
            "3. HISTORIA PROFUNDA: 1 tema para entender a fondo hoy (4-5 frases con contexto)\n" +
            "4. EN DESARROLLO: 1-2 situaciones activas que vigilar (2 frases cada una)\n" +
            "5. SORPRESA: 1 noticia inesperada o poco cubierta que merece atención (2 frases)\n\n" +
            "Todo en español, tono profesional pero accesible. NO uses bullet points genéricos.\n" +
            "Escribe como si hablaras con un ejecutivo inteligente que tiene 3 minutos.\n\n" +
            "Responde en JSON:\n" +
            "{\"headline\": \"...\",\n" +
            " \"top_stories\": \"Historia 1: ...\\n\\nHistoria 2: ...\\n\\nHistoria 3: ...\",\n" +
            " \"deep_dive\": \"...\",\n" +
            " \"developing\": \"...\",\n" +
            " \"surprise\": \"...\"}";

        var response = await ai.CompleteAsync(new AiRequest
        {
            SystemPrompt = "Eres el editor jefe de una agencia de noticias IA. Escribe briefings editoriales concisos, informativos y con personalidad. Responde SOLO con JSON válido.",
            UserPrompt = prompt,
            MaxTokens = 1500,
            Temperature = 0.4,
            OperationTag = "morning_brief"
        }, ct);

        if (!response.Success) return false;
        TrackCostFromResponse(ctx, execution, response);

        try
        {
            var json = ExtractJson(response.Content);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var brief = new MorningBrief
            {
                BriefDate = today,
                Headline = root.GetProperty("headline").GetString() ?? "Briefing del día",
                TopStories = root.GetProperty("top_stories").GetString() ?? "",
                DeepDive = root.TryGetProperty("deep_dive", out var dd) ? dd.GetString() : null,
                Developing = root.TryGetProperty("developing", out var dev) ? dev.GetString() : null,
                Surprise = root.TryGetProperty("surprise", out var sur) ? sur.GetString() : null,
                TopStoriesCount = 3,
                TotalEventsAnalyzed = topEvents.Count,
                GeneratedAt = DateTime.UtcNow
            };
            ctx.MorningBriefs.Add(brief);
            await ctx.SaveChangesAsync(ct);

            await LogAsync(ctx, execution, AgentLogLevel.Info, $"Morning brief generado: {brief.Headline}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[BriefingGenerator] Error parsing morning brief");
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Phase 3: Update story thread summaries
    // ─────────────────────────────────────────────────────────────────────────────
    private async Task<int> UpdateStoryThreadSummariesAsync(
        AppDbContext ctx, IAiProvider ai, AgentExecution execution, CancellationToken ct)
    {
        // Find threads that need summary update (no briefing in 24h, at least 3 events)
        var cutoff = DateTime.UtcNow.AddHours(-24);
        var threads = await ctx.StoryThreads
            .Where(t => (t.Status == StoryStatus.Developing || t.Status == StoryStatus.Active)
                     && t.EventCount >= 3
                     && (t.LastBriefingAt == null || t.LastBriefingAt < cutoff))
            .OrderByDescending(t => t.MaxImpactScore)
            .Take(5)
            .ToListAsync(ct);

        if (threads.Count == 0) return 0;

        int updated = 0;
        foreach (var thread in threads)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var events = await ctx.NewsEvents
                    .Where(e => e.StoryThreadId == thread.Id && !e.IsDeleted)
                    .OrderBy(e => e.StartDate)
                    .Take(20)
                    .Select(e => new { e.Title, e.Description, e.StartDate, e.Location, e.Priority })
                    .ToListAsync(ct);

                var timeline = string.Join("\n", events.Select(e =>
                    $"- {e.StartDate:dd/MM}: {e.Title} ({e.Location}) [{e.Priority}]"));

                var prompt =
                    $"HILO NARRATIVO: {thread.Title}\n" +
                    $"RESUMEN ACTUAL: {thread.Summary ?? "(sin resumen)"}\n\n" +
                    $"EVENTOS ({events.Count}):\n{timeline}\n\n" +
                    "Actualiza el resumen narrativo de este hilo. Escribe como un analista que explica una historia en desarrollo.\n\n" +
                    "Responde en JSON:\n" +
                    "{\"summary\": \"Resumen actualizado de la narrativa (3-4 frases)\",\n" +
                    " \"why_it_matters\": \"Por qué importa seguir esta historia (1-2 frases)\",\n" +
                    " \"what_to_watch\": \"Qué vigilar en los próximos días (1-2 frases)\",\n" +
                    " \"key_actors\": [\"actor1\", \"actor2\", \"actor3\"]}";

                var response = await ai.CompleteAsync(new AiRequest
                {
                    SystemPrompt = "Eres un analista narrativo de noticias. Actualiza resúmenes de hilos narrativos en español. JSON válido.",
                    UserPrompt = prompt,
                    MaxTokens = 600,
                    Temperature = 0.3,
                    OperationTag = "thread_summary"
                }, ct);

                if (!response.Success) continue;
                TrackCostFromResponse(ctx, execution, response);

                var json = ExtractJson(response.Content);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                thread.Summary = root.TryGetProperty("summary", out var s) ? s.GetString() : thread.Summary;
                thread.WhyItMatters = root.TryGetProperty("why_it_matters", out var wim) ? wim.GetString() : null;
                thread.WhatToWatch = root.TryGetProperty("what_to_watch", out var wtw) ? wtw.GetString() : null;
                if (root.TryGetProperty("key_actors", out var actors) && actors.ValueKind == JsonValueKind.Array)
                    thread.KeyActors = actors.EnumerateArray().Select(a => a.GetString() ?? "").Where(a => a.Length > 0).ToArray();

                thread.LastBriefingAt = DateTime.UtcNow;
                await ctx.SaveChangesAsync(ct);
                updated++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[BriefingGenerator] Error updating thread summary {ThreadId}", thread.Id);
            }
        }

        return updated;
    }

    private void TrackCostFromResponse(AppDbContext ctx, AgentExecution execution, AiResponse response)
    {
        if (!execution.AiProviderConfigId.HasValue || response.Cost <= 0) return;
        ctx.CostEntries.Add(new CostEntry
        {
            AiProviderConfigId = execution.AiProviderConfigId.Value,
            AgentExecutionId = execution.Id,
            Operation = "briefing",
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

    private static string Truncate(string s, int max) => s.Length > max ? s[..max] : s;
}
