using ANews.Domain.Entities;
using ANews.Domain.Enums;
using ANews.Domain.Interfaces;
using ANews.Infrastructure.AI;
using ANews.Infrastructure.Agents;
using ANews.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ANews.Web.Endpoints;

public static class AdminApiEndpoints
{
    public static void MapAdminApiEndpoints(this WebApplication app)
    {
        app.MapPost("/api/admin/agents/{type}/trigger", (string type, IServiceProvider sp) =>
        {
            var triggered = type.ToLower() switch
            {
                "newsscanner" => TriggerAgent<NewsScannerAgent>(sp),
                "eventdetector" => TriggerAgent<EventDetectorAgent>(sp),
                "alertgenerator" => TriggerAgent<AlertGeneratorAgent>(sp),
                "notificationdispatcher" => TriggerAgent<NotificationDispatcherAgent>(sp),
                "articlesummarizer" => TriggerAgent<ArticleSummarizerAgent>(sp),
                "digestsender" => TriggerAgent<DigestSenderAgent>(sp),
                "threadweaver" => TriggerAgent<ThreadWeaverAgent>(sp),
                "briefinggenerator" => TriggerAgent<BriefingGeneratorAgent>(sp),
                "sourceanalyzer" => TriggerAgent<SourceAnalyzerAgent>(sp),
                "telegrameditor" => TriggerAgent<TelegramEditorialAgent>(sp),
                "readerprofile" => TriggerAgent<ReaderProfileAgent>(sp),
                _ => false
            };
            return triggered
                ? Results.Ok(new { message = "Agente activado", agent = type })
                : Results.NotFound(new { error = "Tipo de agente no encontrado" });
        }).RequireAuthorization("RequireAdmin");

        app.MapPost("/api/admin/geocode-events", async (AppDbContext ctx, IHttpClientFactory hcf, ILogger<Program> log) =>
        {
            var events = await ctx.NewsEvents
                .Where(e => e.IsActive && !e.IsDeleted && e.Latitude == null && e.Location != null)
                .ToListAsync();

            if (events.Count == 0)
                return Results.Ok(new { message = "No hay eventos sin geocodificar", updated = 0 });

            var client = hcf.CreateClient("nominatim");
            int updated = 0;

            foreach (var ev in events)
            {
                try
                {
                    await Task.Delay(400);
                    var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(ev.Location!)}&format=json&limit=1";
                    var res = await client.GetAsync(url);
                    if (!res.IsSuccessStatusCode) continue;

                    var json = await res.Content.ReadAsStringAsync();
                    using var doc = System.Text.Json.JsonDocument.Parse(json);
                    var arr = doc.RootElement;
                    if (arr.GetArrayLength() > 0)
                    {
                        var first = arr[0];
                        ev.Latitude = double.TryParse(first.GetProperty("lat").GetString(),
                            System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat) ? lat : null;
                        ev.Longitude = double.TryParse(first.GetProperty("lon").GetString(),
                            System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lon) ? lon : null;
                        if (ev.Latitude.HasValue) updated++;
                    }
                }
                catch (Exception ex) { log.LogWarning("Geocode failed for event {Id}: {Err}", ev.Id, ex.Message); }
            }

            await ctx.SaveChangesAsync();
            return Results.Ok(new { message = $"Geocodificados {updated} de {events.Count} eventos", updated });
        }).RequireAuthorization("RequireAdmin");

        app.MapPost("/api/admin/enrich-event-locations", async (AppDbContext ctx, AiProviderFactory aiFactory, IHttpClientFactory hcf, ILogger<Program> log) =>
        {
            var events = await ctx.NewsEvents
                .Where(e => e.IsActive && !e.IsDeleted && e.Location == null)
                .OrderByDescending(e => e.ImpactScore)
                .Take(30)
                .ToListAsync();

            if (events.Count == 0)
                return Results.Ok(new { message = "Todos los eventos ya tienen ubicacion", enriched = 0 });

            IAiProvider ai;
            try { ai = await aiFactory.GetDefaultProviderAsync(); }
            catch { return Results.Ok(new { message = "No hay proveedor de IA configurado", enriched = 0 }); }

            var nominatim = hcf.CreateClient("nominatim");
            int enriched = 0;

            foreach (var ev in events)
            {
                try
                {
                    var aiResp = await ai.CompleteAsync(new AiRequest
                    {
                        SystemPrompt = "You are a geography expert. Reply ONLY with the location name in English (city or country). If the event has no specific geographic location, reply with the single word: null",
                        UserPrompt = $"What is the main geographic location of this news event?\nTitle: {ev.Title}\nDescription: {ev.Description}",
                        MaxTokens = 30,
                        Temperature = 0,
                        OperationTag = "location_enrichment"
                    }, CancellationToken.None);

                    if (!aiResp.Success) continue;

                    var location = aiResp.Content.Trim().Trim('"').Trim('.');
                    if (string.IsNullOrWhiteSpace(location) || location.Equals("null", StringComparison.OrdinalIgnoreCase))
                        continue;

                    ev.Location = location;

                    await Task.Delay(500);
                    var geoUrl = $"search?q={Uri.EscapeDataString(location)}&format=json&limit=1";
                    var geoRes = await nominatim.GetAsync(geoUrl);
                    if (geoRes.IsSuccessStatusCode)
                    {
                        var geoJson = await geoRes.Content.ReadAsStringAsync();
                        using var doc = System.Text.Json.JsonDocument.Parse(geoJson);
                        if (doc.RootElement.GetArrayLength() > 0)
                        {
                            var first = doc.RootElement[0];
                            ev.Latitude = double.TryParse(first.GetProperty("lat").GetString(),
                                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat) ? lat : null;
                            ev.Longitude = double.TryParse(first.GetProperty("lon").GetString(),
                                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lon) ? lon : null;
                        }
                    }

                    enriched++;
                    log.LogInformation("Event {Id} enriched: {Location} ({Lat},{Lng})", ev.Id, ev.Location, ev.Latitude, ev.Longitude);
                }
                catch (Exception ex) { log.LogWarning("Enrich failed for event {Id}: {Err}", ev.Id, ex.Message); }
            }

            await ctx.SaveChangesAsync();
            return Results.Ok(new { message = $"Enriquecidos {enriched} de {events.Count} eventos con ubicacion", enriched });
        }).RequireAuthorization("RequireAdmin");

        app.MapPost("/api/admin/migrate-obsolete-sections", async (AppDbContext ctx, ILogger<Program> log) =>
        {
            var redirects = new Dictionary<string, string>
            {
                ["social"] = "sociedad",
                ["farandula"] = "gente",
                ["ciberguerra"] = "ciberseguridad",
                ["terrorismo"] = "seguridad",
                ["tecnologia-dual"] = "seguridad"
            };

            var allSections = await ctx.NewsSections.IgnoreQueryFilters().ToListAsync();
            var sectionBySlug = allSections.ToDictionary(s => s.Slug);
            int migrated = 0;

            foreach (var (oldSlug, newSlug) in redirects)
            {
                if (!sectionBySlug.TryGetValue(oldSlug, out var oldSection)) continue;
                if (!sectionBySlug.TryGetValue(newSlug, out var newSection)) continue;

                var events = await ctx.NewsEvents.IgnoreQueryFilters()
                    .Where(e => e.NewsSectionId == oldSection.Id)
                    .ToListAsync();

                foreach (var ev in events)
                {
                    ev.NewsSectionId = newSection.Id;
                    migrated++;
                }

                var sources = await ctx.NewsSources.IgnoreQueryFilters()
                    .Where(s => s.NewsSectionId == oldSection.Id)
                    .ToListAsync();

                foreach (var src in sources)
                    src.NewsSectionId = newSection.Id;

                // Soft-delete the obsolete section
                oldSection.IsDeleted = true;
            }

            await ctx.SaveChangesAsync();
            log.LogInformation("Migrated {Count} events from obsolete sections", migrated);
            return Results.Ok(new { message = $"Migrados {migrated} eventos de secciones obsoletas", migrated });
        }).RequireAuthorization("RequireAdmin");

        app.MapPost("/api/admin/reclassify-old", async (AppDbContext ctx, ILogger<Program> log,
            int? olderThanDays, int? batchSize) =>
        {
            var days = olderThanDays ?? 30;
            var batch = Math.Clamp(batchSize ?? 100, 1, 500);
            var ageCutoff = DateTime.UtcNow.AddDays(-days);

            var articles = await ctx.NewsArticles
                .Include(a => a.Event)
                .Where(a => !a.IsDeleted
                    && a.CreatedAt < ageCutoff
                    && a.Event.EventType == "Unclassified")
                .OrderBy(a => a.CreatedAt)
                .Take(batch)
                .ToListAsync();

            if (articles.Count == 0)
                return Results.Ok(new { message = "No hay artículos antiguos para reclasificar", queued = 0 });

            var grouped = articles.GroupBy(a => a.Event.NewsSectionId);
            int queued = 0;
            var today = DateTime.UtcNow.Date;

            foreach (var group in grouped)
            {
                var sectionId = group.Key;
                var unclassifiedEvent = await ctx.NewsEvents
                    .FirstOrDefaultAsync(e => e.NewsSectionId == sectionId
                        && e.EventType == "Unclassified"
                        && e.StartDate >= today);

                if (unclassifiedEvent == null)
                {
                    unclassifiedEvent = new NewsEvent
                    {
                        Title = "Artículos sin clasificar",
                        EventType = "Unclassified",
                        Priority = ANews.Domain.Enums.EventPriority.Low,
                        NewsSectionId = sectionId,
                        StartDate = DateTime.UtcNow,
                        IsActive = false
                    };
                    ctx.NewsEvents.Add(unclassifiedEvent);
                    await ctx.SaveChangesAsync();
                }

                foreach (var article in group)
                {
                    article.NewsEventId = unclassifiedEvent.Id;
                    article.CreatedAt = DateTime.UtcNow; // Reset so EventDetector 72h filter picks them up
                    queued++;
                }
            }

            await ctx.SaveChangesAsync();
            log.LogInformation("Reclassify-old: encolados {Count} artículos > {Days} días", queued, days);
            return Results.Ok(new { message = $"Encolados {queued} artículos para reclasificación", queued, olderThanDays = days, batchSize = batch });
        }).RequireAuthorization("RequireAdmin");
    }

    private static bool TriggerAgent<T>(IServiceProvider sp) where T : BaseAgent
    {
        var agent = sp.GetService<T>();
        if (agent == null) return false;
        agent.TriggerNow();
        return true;
    }
}
