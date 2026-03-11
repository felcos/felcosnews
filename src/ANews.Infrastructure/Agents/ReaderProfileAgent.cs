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

public class ReaderProfileAgent : BaseAgent
{
    protected override AgentType AgentType => AgentType.ReaderProfileAnalyzer;
    protected override string AgentName => "ReaderProfileAgent";
    protected override TimeSpan DefaultInterval => TimeSpan.FromHours(6);

    public ReaderProfileAgent(IServiceProvider services, ILogger<ReaderProfileAgent> logger)
        : base(services, logger) { }

    protected override async Task RunCycleAsync(IServiceProvider services, AgentExecution execution, CancellationToken ct)
    {
        var ctx = services.GetRequiredService<AppDbContext>();
        var aiFactory = services.GetRequiredService<AiProviderFactory>();

        // Find users with >= 10 activities since their last analysis (or ever)
        var profiles = await ctx.ReaderProfiles.ToListAsync(ct);
        var profilesByUser = profiles.ToDictionary(p => p.UserId);

        var userActivityCounts = await ctx.UserActivities
            .GroupBy(a => a.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                TotalCount = g.Count(),
                LatestActivity = g.Max(a => a.CreatedAt)
            })
            .ToListAsync(ct);

        var eligibleUsers = new List<int>();
        foreach (var ua in userActivityCounts)
        {
            if (profilesByUser.TryGetValue(ua.UserId, out var profile))
            {
                // Count activities since last analysis
                var sinceCount = await ctx.UserActivities
                    .CountAsync(a => a.UserId == ua.UserId
                        && a.CreatedAt > (profile.LastAnalyzedAt ?? DateTime.MinValue), ct);
                if (sinceCount >= 10)
                    eligibleUsers.Add(ua.UserId);
            }
            else if (ua.TotalCount >= 10)
            {
                eligibleUsers.Add(ua.UserId);
            }
        }

        if (eligibleUsers.Count == 0)
        {
            await LogAsync(ctx, execution, AgentLogLevel.Info, "Sin usuarios elegibles para análisis de perfil");
            return;
        }

        await LogAsync(ctx, execution, AgentLogLevel.Info,
            $"Analizando perfiles de {eligibleUsers.Count} usuarios");

        // Try to get AI provider (optional)
        IAiProvider? aiProvider = null;
        try
        {
            aiProvider = await aiFactory.GetDefaultProviderAsync();
            var providerConfig = await ctx.AiProviderConfigs.FirstOrDefaultAsync(p => p.IsDefault && p.IsActive, ct);
            if (providerConfig != null) execution.AiProviderConfigId = providerConfig.Id;
        }
        catch (Exception ex)
        {
            await LogAsync(ctx, execution, AgentLogLevel.Warning,
                $"Sin proveedor IA, solo análisis estadístico: {ex.Message}");
        }

        int processed = 0;
        foreach (var userId in eligibleUsers)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                await AnalyzeUserProfileAsync(ctx, aiProvider, execution, userId, profilesByUser, ct);
                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ReaderProfileAgent] Error analizando usuario {UserId}", userId);
            }
        }

        execution.ItemsProcessed = processed;
        await LogAsync(ctx, execution, AgentLogLevel.Info,
            $"Perfiles actualizados: {processed}/{eligibleUsers.Count}");
    }

    private async Task AnalyzeUserProfileAsync(
        AppDbContext ctx, IAiProvider? ai, AgentExecution execution,
        int userId, Dictionary<int, ReaderProfile> profilesByUser, CancellationToken ct)
    {
        // Load last 200 activities for the user
        var activities = await ctx.UserActivities
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        if (activities.Count == 0) return;

        // ── Statistical analysis (always runs) ──────────────────────────────────
        int articlesRead = activities.Count(a => a.NewsArticleId.HasValue);
        int eventsOpened = activities.Count(a => a.NewsEventId.HasValue);
        var lastActivityAt = activities.Max(a => a.CreatedAt);

        // Top 5 sections by activity count
        var topSections = activities
            .Where(a => a.NewsSectionId.HasValue)
            .GroupBy(a => a.NewsSectionId!.Value)
            .OrderByDescending(g => g.Count())
            .Take(5)
            .Select(g => g.Key)
            .ToList();

        var sectionNames = await ctx.NewsSections
            .Where(s => topSections.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var topInterests = topSections
            .Where(id => sectionNames.ContainsKey(id))
            .Select(id => sectionNames[id])
            .ToArray();

        // ── Upsert profile ──────────────────────────────────────────────────────
        ReaderProfile profile;
        if (profilesByUser.TryGetValue(userId, out var existing))
        {
            profile = existing;
        }
        else
        {
            profile = new ReaderProfile { UserId = userId };
            ctx.ReaderProfiles.Add(profile);
        }

        profile.ArticlesRead = articlesRead;
        profile.EventsOpened = eventsOpened;
        profile.LastActivityAt = lastActivityAt;
        profile.TopInterests = topInterests;
        profile.LastAnalyzedAt = DateTime.UtcNow;

        // ── AI analysis (optional) ──────────────────────────────────────────────
        if (ai != null)
        {
            await EnrichWithAiAsync(ctx, ai, execution, profile, activities, topInterests, ct);
        }

        await ctx.SaveChangesAsync(ct);
    }

    private async Task EnrichWithAiAsync(
        AppDbContext ctx, IAiProvider ai, AgentExecution execution,
        ReaderProfile profile, List<UserActivity> activities,
        string[] topInterests, CancellationToken ct)
    {
        // Build activity summary for AI
        var sectionActivity = activities
            .Where(a => a.NewsSectionId.HasValue)
            .GroupBy(a => a.NewsSectionId!.Value)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .ToList();

        var sectionIds = sectionActivity.Select(g => g.Key).ToList();
        var sectionMap = await ctx.NewsSections
            .Where(s => sectionIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s.Name, ct);

        var activitySummary = string.Join("\n", sectionActivity
            .Where(g => sectionMap.ContainsKey(g.Key))
            .Select(g => $"- {sectionMap[g.Key]}: {g.Count()} actividades"));

        var articleActivities = activities.Where(a => a.NewsArticleId.HasValue).ToList();
        var eventActivities = activities.Where(a => a.NewsEventId.HasValue).ToList();

        var prompt =
            $"PERFIL DE LECTURA DEL USUARIO (ID: {profile.UserId})\n\n" +
            $"Total de actividades analizadas: {activities.Count}\n" +
            $"Artículos leídos: {articleActivities.Count}\n" +
            $"Eventos abiertos: {eventActivities.Count}\n" +
            $"Intereses principales: {string.Join(", ", topInterests)}\n\n" +
            $"ACTIVIDAD POR SECCIÓN:\n{activitySummary}\n\n" +
            "Analiza el comportamiento de lectura de este usuario y responde en JSON:\n" +
            "{\"semantic_profile\": \"Descripción de 2-3 frases del patrón de lectura del usuario: qué temas prefiere, con qué frecuencia lee, qué tipo de contenido consume\",\n" +
            " \"avoid_topics\": [\"tema1\", \"tema2\"],\n" +
            " \"preferred_depth\": \"quick|medium|deep\"}\n\n" +
            "Para avoid_topics: identifica temas o secciones que el usuario claramente evita a pesar de estar disponibles.\n" +
            "Para preferred_depth: 'quick' si mayormente abre eventos sin leer artículos, 'deep' si lee muchos artículos por evento, 'medium' si es intermedio.";

        try
        {
            var response = await ai.CompleteAsync(new AiRequest
            {
                SystemPrompt = "Eres un analista de comportamiento de lectura. Analiza patrones de consumo de noticias y genera perfiles semánticos precisos en español. Responde SOLO con JSON válido.",
                UserPrompt = prompt,
                MaxTokens = 500,
                Temperature = 0.3,
                OperationTag = "reader_profile"
            }, ct);

            if (!response.Success) return;
            TrackCostFromResponse(ctx, execution, response);

            var json = ExtractJson(response.Content);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("semantic_profile", out var sp))
                profile.SemanticProfile = sp.GetString();

            if (root.TryGetProperty("avoid_topics", out var at) && at.ValueKind == JsonValueKind.Array)
                profile.AvoidTopics = at.EnumerateArray()
                    .Select(a => a.GetString() ?? "")
                    .Where(a => a.Length > 0)
                    .ToArray();

            if (root.TryGetProperty("preferred_depth", out var pd))
                profile.PreferredDepth = pd.GetString();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[ReaderProfileAgent] Error en análisis IA para usuario {UserId}", profile.UserId);
        }
    }

    private void TrackCostFromResponse(AppDbContext ctx, AgentExecution execution, AiResponse response)
    {
        if (!execution.AiProviderConfigId.HasValue || response.Cost <= 0) return;
        ctx.CostEntries.Add(new CostEntry
        {
            AiProviderConfigId = execution.AiProviderConfigId.Value,
            AgentExecutionId = execution.Id,
            Operation = "reader_profile",
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
