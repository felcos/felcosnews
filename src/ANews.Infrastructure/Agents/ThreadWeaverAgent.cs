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

public class ThreadWeaverAgent : BaseAgent
{
    protected override AgentType AgentType => AgentType.ThreadWeaver;
    protected override string AgentName => "ThreadWeaverAgent";
    protected override TimeSpan DefaultInterval => TimeSpan.FromHours(3);

    public ThreadWeaverAgent(IServiceProvider services, ILogger<ThreadWeaverAgent> logger)
        : base(services, logger) { }

    protected override async Task RunCycleAsync(IServiceProvider services, AgentExecution execution, CancellationToken ct)
    {
        var ctx = services.GetRequiredService<AppDbContext>();
        var aiFactory = services.GetRequiredService<AiProviderFactory>();

        // 1. Find events not yet linked to a story thread (last 7 days, non-trivial)
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var orphanEvents = await ctx.NewsEvents
            .Include(e => e.Section)
            .Where(e => e.StoryThreadId == null
                     && e.IsActive
                     && e.EventType == "Detected"
                     && e.CreatedAt >= cutoff)
            .OrderByDescending(e => e.ImpactScore)
            .Take(100)
            .ToListAsync(ct);

        if (orphanEvents.Count == 0)
        {
            await LogAsync(ctx, execution, AgentLogLevel.Info, "No hay eventos huérfanos para vincular a hilos narrativos");
            await UpdateStoryThreadStats(ctx, ct);
            return;
        }

        await LogAsync(ctx, execution, AgentLogLevel.Info, $"Procesando {orphanEvents.Count} eventos sin hilo narrativo");

        // 2. Load existing active story threads
        var activeThreads = await ctx.StoryThreads
            .Where(t => t.Status == StoryStatus.Developing || t.Status == StoryStatus.Active)
            .OrderByDescending(t => t.LastEventDate)
            .Take(50)
            .ToListAsync(ct);

        IAiProvider aiProvider;
        try { aiProvider = await aiFactory.GetDefaultProviderAsync(); }
        catch (Exception ex)
        {
            await LogAsync(ctx, execution, AgentLogLevel.Error, $"Sin proveedor IA: {ex.Message}");
            return;
        }

        var providerConfig = await ctx.AiProviderConfigs.FirstOrDefaultAsync(p => p.IsDefault && p.IsActive, ct);
        if (providerConfig != null) execution.AiProviderConfigId = providerConfig.Id;

        // 3. Process in batches of 30
        int linked = 0, created = 0;
        var batches = orphanEvents.Chunk(30).ToList();

        foreach (var batch in batches)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var result = await LinkEventsToThreadsAsync(ctx, aiProvider, batch.ToList(), activeThreads, execution, ct);
                linked += result.Linked;
                created += result.Created;

                // Refresh active threads after creating new ones
                if (result.Created > 0)
                {
                    activeThreads = await ctx.StoryThreads
                        .Where(t => t.Status == StoryStatus.Developing || t.Status == StoryStatus.Active)
                        .OrderByDescending(t => t.LastEventDate)
                        .Take(50)
                        .ToListAsync(ct);
                }
            }
            catch (Exception ex)
            {
                await LogAsync(ctx, execution, AgentLogLevel.Warning, $"Error en lote: {ex.Message}");
            }
        }

        // 4. Update cross-reference counts and source diversity
        await UpdateEventMetrics(ctx, ct);

        // 5. Update story thread stats
        await UpdateStoryThreadStats(ctx, ct);

        execution.ItemsProcessed = orphanEvents.Count;
        execution.ItemsCreated = created;
        await LogAsync(ctx, execution, AgentLogLevel.Info,
            $"Vinculados {linked} eventos a hilos existentes, {created} hilos nuevos creados");
    }

    private async Task<(int Linked, int Created)> LinkEventsToThreadsAsync(
        AppDbContext ctx, IAiProvider ai,
        List<NewsEvent> events, List<StoryThread> threads,
        AgentExecution execution, CancellationToken ct)
    {
        // Build event descriptions
        var eventLines = events.Select((e, i) =>
            $"{i + 1}. [{e.Section?.Name}] {e.Title} | {e.Description?[..Math.Min(e.Description?.Length ?? 0, 150)]} | Loc:{e.Location}")
            .ToList();

        // Build thread descriptions
        var threadLines = threads.Select((t, i) =>
            $"T{i + 1}. {t.Title} | Status:{t.Status} | Eventos:{t.EventCount} | Última actividad:{t.LastEventDate:dd/MM}")
            .ToList();

        var prompt =
            "Eres el editor narrativo de una agencia de noticias. Tu tarea es vincular eventos a HILOS NARRATIVOS existentes o crear nuevos.\n\n" +
            "Un HILO NARRATIVO es una historia en desarrollo que conecta múltiples eventos:\n" +
            "- 'Guerra en Ucrania' conecta ataques, diplomacia, sanciones, refugiados\n" +
            "- 'Crisis económica Argentina' conecta inflación, devaluación, medidas gobierno\n" +
            "- 'Regulación IA en UE' conecta leyes, debates, reacciones empresas\n\n" +
            "HILOS EXISTENTES:\n" +
            (threadLines.Any() ? string.Join("\n", threadLines) : "(ninguno)") + "\n\n" +
            "EVENTOS A VINCULAR:\n" +
            string.Join("\n", eventLines) + "\n\n" +
            "REGLAS:\n" +
            "1. Vincula al hilo existente SOLO si el evento es claramente un capítulo de esa historia\n" +
            "2. Crea nuevo hilo SOLO si hay 2+ eventos que forman una narrativa coherente\n" +
            "3. Eventos aislados sin relación → 'none'\n" +
            "4. No fuerces conexiones — mejor 'none' que una vinculación incorrecta\n" +
            "5. Para hilos nuevos, da un título descriptivo y duradero (no temporal)\n\n" +
            "Responde SOLO con JSON:\n" +
            "{\"links\": [{\"event\": 1, \"thread\": \"T3\"}, {\"event\": 2, \"thread\": \"none\"}],\n" +
            " \"new_threads\": [{\"title\": \"...\", \"summary\": \"...\", \"events\": [4, 7]}]}";

        var response = await ai.CompleteAsync(new AiRequest
        {
            SystemPrompt = "Eres un editor narrativo experto. Conecta eventos en hilos de historia coherentes. Responde SOLO con JSON válido.",
            UserPrompt = prompt,
            MaxTokens = 2000,
            Temperature = 0.1,
            OperationTag = "thread_weaving"
        }, ct);

        if (!response.Success) return (0, 0);

        TrackCostFromResponse(ctx, execution, response);

        int linked = 0, created = 0;

        try
        {
            var json = ExtractJson(response.Content);
            using var doc = JsonDocument.Parse(json);

            // Process links to existing threads
            if (doc.RootElement.TryGetProperty("links", out var links))
            {
                foreach (var link in links.EnumerateArray())
                {
                    var eventIdx = link.GetProperty("event").GetInt32() - 1;
                    var threadRef = link.GetProperty("thread").GetString() ?? "none";

                    if (threadRef == "none" || eventIdx < 0 || eventIdx >= events.Count) continue;

                    // Parse T{n} reference
                    if (threadRef.StartsWith("T") && int.TryParse(threadRef[1..], out var tIdx))
                    {
                        tIdx--; // 1-based
                        if (tIdx >= 0 && tIdx < threads.Count)
                        {
                            events[eventIdx].StoryThreadId = threads[tIdx].Id;
                            threads[tIdx].LastEventDate = DateTime.UtcNow;
                            threads[tIdx].EventCount++;
                            if (events[eventIdx].Priority > threads[tIdx].MaxPriority)
                                threads[tIdx].MaxPriority = events[eventIdx].Priority;
                            linked++;
                        }
                    }
                }
            }

            // Process new threads
            if (doc.RootElement.TryGetProperty("new_threads", out var newThreads))
            {
                foreach (var nt in newThreads.EnumerateArray())
                {
                    var title = nt.GetProperty("title").GetString();
                    var summary = nt.TryGetProperty("summary", out var s) ? s.GetString() : null;
                    var eventIndices = nt.GetProperty("events").EnumerateArray()
                        .Select(e => e.GetInt32() - 1)
                        .Where(i => i >= 0 && i < events.Count)
                        .ToList();

                    if (string.IsNullOrEmpty(title) || eventIndices.Count == 0) continue;

                    var thread = new StoryThread
                    {
                        Title = title,
                        Summary = summary,
                        Status = StoryStatus.Developing,
                        FirstEventDate = eventIndices.Min(i => events[i].StartDate),
                        LastEventDate = DateTime.UtcNow,
                        EventCount = eventIndices.Count,
                        MaxPriority = eventIndices.Max(i => events[i].Priority),
                        MaxImpactScore = eventIndices.Max(i => events[i].ImpactScore),
                        PrimarySectionId = events[eventIndices[0]].NewsSectionId,
                        Tags = eventIndices.SelectMany(i => events[i].Tags).Distinct().Take(10).ToArray()
                    };
                    ctx.StoryThreads.Add(thread);
                    await ctx.SaveChangesAsync(ct);

                    foreach (var idx in eventIndices)
                        events[idx].StoryThreadId = thread.Id;

                    created++;
                }
            }

            await ctx.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ThreadWeaver] Error parsing AI response");
        }

        return (linked, created);
    }

    private async Task UpdateEventMetrics(AppDbContext ctx, CancellationToken ct)
    {
        // Update CrossReferenceCount and SourceDiversity for recent events
        var recentEvents = await ctx.NewsEvents
            .Include(e => e.Articles)
            .Where(e => e.IsActive && e.CreatedAt >= DateTime.UtcNow.AddDays(-3))
            .ToListAsync(ct);

        foreach (var ev in recentEvents)
        {
            var articles = ev.Articles.Where(a => !a.IsDeleted).ToList();
            ev.CrossReferenceCount = articles.Count;
            ev.SourceDiversity = articles.Select(a => a.SourceName).Distinct().Count();
        }

        await ctx.SaveChangesAsync(ct);
    }

    private async Task UpdateStoryThreadStats(AppDbContext ctx, CancellationToken ct)
    {
        var threads = await ctx.StoryThreads
            .Where(t => t.Status == StoryStatus.Developing || t.Status == StoryStatus.Active)
            .ToListAsync(ct);

        foreach (var thread in threads)
        {
            var eventCount = await ctx.NewsEvents.CountAsync(e => e.StoryThreadId == thread.Id && !e.IsDeleted, ct);
            var articleCount = await ctx.NewsArticles
                .CountAsync(a => a.Event.StoryThreadId == thread.Id && !a.IsDeleted, ct);
            var lastEvent = await ctx.NewsEvents
                .Where(e => e.StoryThreadId == thread.Id && !e.IsDeleted)
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefaultAsync(ct);

            thread.EventCount = eventCount;
            thread.TotalArticles = articleCount;
            if (lastEvent != null) thread.LastEventDate = lastEvent.CreatedAt;

            // Auto-stale if no new events in 3 days
            if (thread.Status == StoryStatus.Developing && (DateTime.UtcNow - thread.LastEventDate).TotalDays > 3)
                thread.Status = StoryStatus.Stale;
            // Auto-archive if stale for 7 more days
            if (thread.Status == StoryStatus.Stale && (DateTime.UtcNow - thread.LastEventDate).TotalDays > 10)
                thread.Status = StoryStatus.Archived;
        }

        await ctx.SaveChangesAsync(ct);
    }

    private void TrackCostFromResponse(AppDbContext ctx, AgentExecution execution, AiResponse response)
    {
        if (!execution.AiProviderConfigId.HasValue || response.Cost <= 0) return;
        ctx.CostEntries.Add(new CostEntry
        {
            AiProviderConfigId = execution.AiProviderConfigId.Value,
            AgentExecutionId = execution.Id,
            Operation = "thread_weaving",
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
