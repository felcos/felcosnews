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
    protected override TimeSpan DefaultInterval => TimeSpan.FromHours(2);

    public EventDetectorAgent(IServiceProvider services, ILogger<EventDetectorAgent> logger)
        : base(services, logger) { }

    protected override async Task RunCycleAsync(IServiceProvider services, AgentExecution execution, CancellationToken ct)
    {
        var ctx = services.GetRequiredService<AppDbContext>();
        var aiFactory = services.GetRequiredService<AiProviderFactory>();

        var cutoff = DateTime.UtcNow.AddHours(-72);
        var maxItems = 300;

        var allUnclassified = await ctx.NewsArticles
            .Include(a => a.Event).ThenInclude(e => e.Section)
            .Where(a => a.Event.EventType == "Unclassified" && a.CreatedAt >= cutoff)
            .OrderByDescending(a => a.CreatedAt)
            .Take(maxItems)
            .ToListAsync(ct);

        if (allUnclassified.Count == 0)
        {
            await LogAsync(ctx, execution, AgentLogLevel.Info, "No hay artículos sin clasificar");
            await GeocodeEventsMissingCoordsAsync(services, ctx, execution, ct);
            return;
        }

        await LogAsync(ctx, execution, AgentLogLevel.Info, $"Procesando {allUnclassified.Count} artículos");

        IAiProvider aiProvider;
        try { aiProvider = await aiFactory.GetDefaultProviderAsync(); }
        catch (Exception ex)
        {
            await LogAsync(ctx, execution, AgentLogLevel.Error, $"Sin proveedor IA: {ex.Message}");
            return;
        }

        var providerConfig = await ctx.AiProviderConfigs.FirstOrDefaultAsync(p => p.IsDefault && p.IsActive, ct);
        if (providerConfig != null) execution.AiProviderConfigId = providerConfig.Id;

        // Load all sections for routing
        var sections = await ctx.NewsSections.Where(s => !s.IsDeleted).ToListAsync(ct);

        // ── Phase 1: Route articles to correct sections ─────────────────────────
        await LogAsync(ctx, execution, AgentLogLevel.Info, "Fase 1: clasificando artículos en secciones correctas...");
        int rerouted = await ReclassifyArticlesBySectionAsync(ctx, aiProvider, allUnclassified, sections, execution, ct);
        await LogAsync(ctx, execution, AgentLogLevel.Info, $"Fase 1 completa: {rerouted} artículos reubicados en su sección correcta");

        // Re-read after reclassification so sections reflect new assignments
        var reclassified = await ctx.NewsArticles
            .Include(a => a.Event).ThenInclude(e => e.Section)
            .Where(a => a.Event.EventType == "Unclassified" && a.CreatedAt >= cutoff)
            .OrderByDescending(a => a.CreatedAt)
            .Take(maxItems)
            .ToListAsync(ct);

        // ── Phase 2: Cluster into events per section ─────────────────────────────
        var bySection = reclassified
            .GroupBy(a => a.Event.NewsSectionId)
            .ToList();

        int totalClusters = 0, totalProcessed = 0;

        foreach (var sectionGroup in bySection)
        {
            if (ct.IsCancellationRequested) break;

            var articles = sectionGroup.ToList();
            var section = sections.FirstOrDefault(s => s.Id == sectionGroup.Key);
            var sectionName = section?.Name ?? $"Sección {sectionGroup.Key}";

            var batches = articles.Chunk(60).ToList();
            foreach (var batch in batches)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    await LogAsync(ctx, execution, AgentLogLevel.Info,
                        $"[{sectionName}] Agrupando {batch.Length} artículos en eventos...");

                    var clusters = await ClusterArticlesWithAiAsync(
                        aiProvider, batch.ToList(), section, execution, ctx, ct);

                    foreach (var cluster in clusters)
                        await CreateOrUpdateEventAsync(ctx, cluster, execution, ct);

                    totalClusters += clusters.Count;
                    totalProcessed += batch.Length;

                    TrackCost(ctx, execution, providerConfig);
                }
                catch (Exception ex)
                {
                    await LogAsync(ctx, execution, AgentLogLevel.Warning,
                        $"[{sectionName}] Error en lote: {ex.Message}");
                }
            }
        }

        execution.ItemsProcessed = totalProcessed;
        execution.ItemsCreated = totalClusters;
        await LogAsync(ctx, execution, AgentLogLevel.Info,
            $"Detectados {totalClusters} eventos de {totalProcessed} artículos ({bySection.Count} secciones)");

        await GeocodeEventsMissingCoordsAsync(services, ctx, execution, ct);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  PHASE 1 — Section router
    //  Takes articles from ANY section and moves them to the correct one based on content
    // ─────────────────────────────────────────────────────────────────────────────
    private async Task<int> ReclassifyArticlesBySectionAsync(
        AppDbContext ctx,
        IAiProvider ai,
        List<NewsArticle> articles,
        List<NewsSection> sections,
        AgentExecution execution,
        CancellationToken ct)
    {
        // Build section descriptor for the prompt
        var sectionDesc = BuildSectionDescriptor(sections);

        // Cache of unclassified events per section
        var unclassifiedEventCache = new Dictionary<int, NewsEvent>();

        int totalRerouted = 0;

        // Batch: 40 articles per AI call (titles + summaries)
        var batches = articles.Chunk(40).ToList();
        foreach (var batch in batches)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                var articleLines = batch
                    .Select((a, i) =>
                    {
                        var summary = (a.Summary ?? a.Content ?? "");
                        if (summary.Length > 200) summary = summary[..200];
                        return $"{i + 1}. [{a.SourceName}] {a.Title}" +
                               (string.IsNullOrWhiteSpace(summary) ? "" : $"\n   > {summary}");
                    })
                    .ToList();

                var prompt =
                    "Eres el jefe de clasificacion de una agencia de noticias mundial.\n" +
                    "Tu unica tarea: asignar CADA articulo a la seccion mas apropiada.\n" +
                    "LEE CUIDADOSAMENTE el titulo Y el resumen para decidir la seccion correcta.\n\n" +
                    "SECCIONES DISPONIBLES (slug: descripcion):\n" +
                    sectionDesc + "\n\n" +
                    "REGLAS CRITICAS DE CLASIFICACION (lee el contenido, no solo el titulo):\n" +
                    "- Guerras, conflictos armados, diplomacia, relaciones entre paises → mundo\n" +
                    "- Politica nacional (elecciones, gobierno, leyes, partidos) → politica\n" +
                    "- Empresas, mercados, finanzas, inmobiliario, empleo, startups → economia\n" +
                    "- IA, software, hardware, apps, redes sociales, innovacion → tecnologia\n" +
                    "- Investigacion, medicina, epidemias, espacio, farmaceutica → ciencia\n" +
                    "- Educacion, inmigracion, derechos humanos, religion, genero → sociedad\n" +
                    "- Terrorismo, crimen organizado, ciberataques, hackeos, ejercitos, armas, espionaje → seguridad\n" +
                    "- Asesinatos, violaciones, corrupcion, tribunales, sentencias, juicios → justicia\n" +
                    "- Clima, desastres naturales, energia, contaminacion, biodiversidad → medioambiente\n" +
                    "- Arte, cine, musica, literatura, deportes, competiciones, premios → cultura\n" +
                    "- Celebrities, prensa rosa, realeza, bodas de famosos, reality TV, farandula → gente\n" +
                    "- Editoriales, columnas de opinion, analisis de fondo → opinion\n" +
                    "- Breaking news, emergencias en desarrollo, alertas urgentes → ultimahora\n\n" +
                    "ERRORES COMUNES A EVITAR:\n" +
                    "- Un ataque militar es 'seguridad' o 'mundo', NO 'tecnologia'\n" +
                    "- Un juicio penal es 'justicia', NO 'politica' (salvo que sea sobre politicos)\n" +
                    "- Una epidemia es 'ciencia', NO 'sociedad'\n" +
                    "- Un hackeo/ciberataque es 'seguridad', NO 'tecnologia'\n" +
                    "- Un desastre natural es 'medioambiente', NO 'mundo'\n" +
                    "- Deportes y arte van a 'cultura', NO a 'sociedad'\n\n" +
                    "ARTICULOS:\n" +
                    string.Join("\n", articleLines) + "\n\n" +
                    "Responde SOLO con JSON valido: {\"assignments\": {\"1\": \"slug\", \"2\": \"slug\", ...}}";

                var response = await ai.CompleteAsync(new AiRequest
                {
                    SystemPrompt = "Eres un clasificador de noticias experto. Lee TITULO Y RESUMEN de cada articulo para clasificar correctamente. Responde SOLO con JSON válido, sin markdown.",
                    UserPrompt = prompt,
                    MaxTokens = 1500,
                    Temperature = 0.0,
                    OperationTag = "section_routing"
                }, ct);

                if (!response.Success) continue;

                TrackCostFromResponse(ctx, execution, response);

                // Parse assignments
                var json = ExtractJson(response.Content);
                using var doc = JsonDocument.Parse(json);
                if (!doc.RootElement.TryGetProperty("assignments", out var assignments)) continue;

                foreach (var prop in assignments.EnumerateObject())
                {
                    if (!int.TryParse(prop.Name, out var idx)) continue;
                    idx--; // 1-based → 0-based
                    if (idx < 0 || idx >= batch.Length) continue;

                    var article = batch[idx];
                    var targetSlug = prop.Value.GetString()?.Trim().ToLowerInvariant();
                    if (string.IsNullOrEmpty(targetSlug)) continue;

                    var targetSection = sections.FirstOrDefault(s =>
                        s.Slug.Equals(targetSlug, StringComparison.OrdinalIgnoreCase));
                    if (targetSection == null) continue;

                    // Only move if section actually differs
                    if (article.Event?.NewsSectionId == targetSection.Id) continue;

                    // Get or create "Unclassified" event for target section
                    if (!unclassifiedEventCache.TryGetValue(targetSection.Id, out var targetEvent))
                    {
                        targetEvent = await GetOrCreateUnclassifiedEventAsync(ctx, targetSection.Id, ct);
                        unclassifiedEventCache[targetSection.Id] = targetEvent;
                    }

                    article.NewsEventId = targetEvent.Id;
                    totalRerouted++;
                }

                await ctx.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                await LogAsync(ctx, execution, AgentLogLevel.Warning,
                    $"[SectionRouter] Error en lote: {ex.Message}");
            }
        }

        return totalRerouted;
    }

    private static string BuildSectionDescriptor(List<NewsSection> sections)
    {
        var lines = sections
            .OrderBy(s => s.SortOrder)
            .Select(s => $"- {s.Slug}: {s.Description ?? s.Name}")
            .ToList();
        return string.Join("\n", lines);
    }

    private async Task<NewsEvent> GetOrCreateUnclassifiedEventAsync(AppDbContext ctx, int sectionId, CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;
        var existing = await ctx.NewsEvents.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.NewsSectionId == sectionId
                                   && e.EventType == "Unclassified"
                                   && e.StartDate >= today, ct);
        if (existing != null) return existing;

        var ev = new NewsEvent
        {
            Title = "Artículos sin clasificar",
            EventType = "Unclassified",
            Priority = EventPriority.Low,
            NewsSectionId = sectionId,
            StartDate = DateTime.UtcNow,
            IsActive = false
        };
        ctx.NewsEvents.Add(ev);
        await ctx.SaveChangesAsync(ct);
        return ev;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  PHASE 2 — Event clusterer (within a section)
    // ─────────────────────────────────────────────────────────────────────────────
    private async Task<List<ArticleCluster>> ClusterArticlesWithAiAsync(
        IAiProvider ai,
        List<NewsArticle> articles,
        NewsSection? section,
        AgentExecution execution,
        AppDbContext ctx,
        CancellationToken ct)
    {
        var sectionName = section?.Name ?? "General";
        var sectionDesc = section?.Description ?? "";
        var articlesList = articles.Select((a, i) => $"{i + 1}. [{a.SourceName}] {a.Title}").ToList();
        var articlesText = string.Join("\n", articlesList);

        var sectionContext = BuildSectionContext(section);

        var prompt =
            $"{sectionContext}\n\n" +
            "Eres editor jefe de una agencia de noticias internacional. " +
            "Agrupa estas noticias en EVENTOS DISTINTOS con precisión periodística.\n\n" +
            "REGLAS CRÍTICAS DE AGRUPACIÓN:\n" +
            "1. Un evento = un hecho específico en un lugar y tiempo concreto\n" +
            "2. NO mezcles eventos de países distintos aunque sean del mismo tema\n" +
            "3. Agrupa SOLO si los artículos hablan EXACTAMENTE del mismo hecho\n" +
            "4. Un artículo solo es válido como evento individual — no fuerces agrupaciones\n\n" +
            "UBICACIÓN (CRÍTICO):\n" +
            "- Ciudad o país MÁS ESPECÍFICO posible\n" +
            "- 'Gaza Strip' NO 'Middle East' · 'Caracas' NO 'Venezuela'\n" +
            "- Regiones solo si afecta múltiples países sin epicentro claro\n\n" +
            "PRIORIDAD: Critical=crisis global activa / High=multinacional / Medium=nacional / Low=local\n" +
            "IMPACT_SCORE 0-100: 90+=histórico · 70-89=crisis nacional · 50-69=importante · 20-49=regional · 0-19=local\n" +
            $"CATEGORÍA obligatoria = una de: {GetCategoryList(section)}\n\n" +
            "Noticias:\n" + articlesText + "\n\n" +
            "Responde SOLO con JSON válido:\n" +
            "{\"events\": [{\"title\": \"...\", \"description\": \"...\", \"priority\": \"High\", " +
            "\"impact_score\": 75, \"category\": \"...\", \"location\": \"Madrid, Spain\", " +
            "\"latitude\": 40.4168, \"longitude\": -3.7038, \"article_indices\": [1, 3]}]}";

        var response = await ai.CompleteAsync(new AiRequest
        {
            SystemPrompt = $"Eres un editor de noticias experto en la sección '{sectionName}'. " +
                           "Clasifica y agrupa noticias en eventos coherentes. Responde SIEMPRE con JSON válido.",
            UserPrompt = prompt,
            MaxTokens = 4000,
            Temperature = 0.1,
            OperationTag = "event_detection"
        }, ct);

        if (!response.Success) return [];

        TrackCostFromResponse(ctx, execution, response);

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
            await LogAsync(ctx, execution, AgentLogLevel.Warning,
                $"[{sectionName}] No se pudo parsear respuesta — usando agrupación básica");
            return FallbackCluster(articles);
        }
    }

    private static string BuildSectionContext(NewsSection? section)
    {
        if (section == null) return "CONTEXTO: Noticias generales.";

        return section.Slug switch
        {
            "mundo" =>
                "SECCION: INTERNACIONAL\n" +
                "Agrupa por: conflicto, relacion bilateral, cumbre o crisis internacional.\n" +
                "Incluye: guerras, diplomacia, sanciones, alianzas, conflictos armados, defensa.\n" +
                "Separa por teatro de operaciones: Ucrania, Gaza, etc. son eventos distintos.",

            "politica" =>
                "SECCION: POLITICA\n" +
                "Agrupa por: pais, partido, cargo o proceso electoral.\n" +
                "Incluye: elecciones, legislacion, dimisiones, escandalos politicos.",

            "economia" =>
                "SECCION: ECONOMIA Y NEGOCIOS\n" +
                "Agrupa por: empresa, indicador, mercado o sector.\n" +
                "Incluye: bolsa, inflacion, empleo, inmobiliario, hipotecas, alquiler, M&A, startups, IPO, quiebras.\n" +
                "Separa macro (indicadores pais) de micro (noticias de empresa concreta).",

            "tecnologia" =>
                "SECCION: TECNOLOGIA\n" +
                "Agrupa por: empresa tech, producto, tendencia o breakthrough.\n" +
                "Incluye: IA, software, hardware, apps, regulacion tech, telecomunicaciones.\n" +
                "NO incluyas ciberataques (van a Seguridad).",

            "ciencia" =>
                "SECCION: CIENCIA Y SALUD\n" +
                "Agrupa por: enfermedad, descubrimiento, mision espacial o estudio.\n" +
                "Incluye: epidemias, medicamentos, investigacion, espacio, fisica, biologia.",

            "sociedad" =>
                "SECCION: SOCIEDAD\n" +
                "Agrupa por: fenomeno social, colectivo afectado o politica publica.\n" +
                "Incluye: educacion, derechos humanos, inmigracion, religion, genero.",

            "seguridad" =>
                "SECCION: SEGURIDAD Y DEFENSA\n" +
                "Agrupa por: tipo de amenaza, operacion, actor o vulnerabilidad.\n" +
                "Incluye: terrorismo, crimen organizado, ciberataques, hackeos, ransomware,\n" +
                "espionaje, armas nucleares/quimicas, ejercitos, operaciones militares.\n" +
                "Separa ciberataques de ataques fisicos como eventos distintos.",

            "justicia" =>
                "SECCION: JUSTICIA\n" +
                "Agrupa por: caso judicial, imputado o tipo de delito.\n" +
                "Incluye: crimenes, juicios, detenciones, condenas, corrupcion, sentencias.",

            "medioambiente" =>
                "SECCION: MEDIO AMBIENTE\n" +
                "Agrupa por: fenomeno climatico, region o politica medioambiental.\n" +
                "Incluye: desastres naturales, cambio climatico, contaminacion, energias renovables.",

            "cultura" =>
                "SECCION: CULTURA Y DEPORTES\n" +
                "Agrupa por: competicion/equipo (deportes) u obra/artista (cultura).\n" +
                "Incluye: futbol, F1, tenis, olimpiadas, cine, musica, literatura, premios, patrimonio.\n" +
                "Un partido = un evento. Una pelicula/serie = un evento.",

            "gente" =>
                "SECCION: GENTE Y CORAZON\n" +
                "Agrupa por: celebrity, escandalo o evento social de famosos.\n" +
                "Incluye: prensa rosa, realeza, bodas, divorcios, reality TV, farandula.\n" +
                "NO incluyas noticias con contenido judicial grave (van a Justicia).",

            "opinion" =>
                "SECCION: OPINION Y ANALISIS\n" +
                "Agrupa por: tema de opinion o columnista.\n" +
                "Incluye: editoriales, columnas, analisis de fondo, tribunas.\n" +
                "Solo si el articulo es CLARAMENTE opinion, no informativo.",

            "ultimahora" =>
                "SECCION: ULTIMA HORA\n" +
                "Solo para breaking news activo — emergencias, atentados en curso, desastres recientes.\n" +
                "Una vez pasa la urgencia, el evento se reclasifica a su seccion permanente.",

            _ =>
                $"SECCION: {section.Name.ToUpperInvariant()}\n{section.Description}"
        };
    }

    private static string GetCategoryList(NewsSection? section) => section?.Slug switch
    {
        "mundo"          => "Diplomacia/Guerra/Sanciones/Cumbre/Conflicto-Armado/Alianza/Tratado/Defensa/Otros",
        "politica"       => "Elecciones/Legislación/Gobierno/Escándalo/Dimisión/Partido-Político/Otros",
        "economia"       => "Mercados/Inflación/Empleo/Inmobiliario/Hipotecas/M&A/Startup/IPO/Quiebra/Banca/Bolsa/Otros",
        "tecnologia"     => "IA/Software/Hardware/Startup/Regulación/Internet/Robótica/Telecomunicaciones/Otros",
        "ciencia"        => "Epidemia/Medicamento/Investigación/Espacio/Física/Biología/Salud-Pública/Otros",
        "sociedad"       => "Educación/Inmigración/Derechos-Humanos/Religión/Demografía/Género/Otros",
        "seguridad"      => "Terrorismo/Crimen-Organizado/Ciberataque/Hackeo/Ransomware/Espionaje/Armas/Operación-Militar/Otros",
        "justicia"       => "Crimen/Corrupción/Juicio/Detención/Investigación/Condena/Sentencia/Otros",
        "medioambiente"  => "Clima/Desastre-Natural/Contaminación/Biodiversidad/Energía-Renovable/Océanos/Otros",
        "cultura"        => "Fútbol/Tenis/F1/Olimpiadas/Cine/Música/Literatura/Arte/Premios/Patrimonio/Otros",
        "gente"          => "Celebrity/Realeza/Prensa-Rosa/Boda/Reality-TV/Farándula/Escándalo/Otros",
        "opinion"        => "Editorial/Columna/Análisis/Tribuna/Perspectiva/Otros",
        "ultimahora"     => "Emergencia/Atentado/Desastre/Crisis/Alerta/Otros",
        _                => "Política/Economía/Sociedad/Tecnología/Seguridad/Otros"
    };

    // ─────────────────────────────────────────────────────────────────────────────
    //  Create or update event from cluster
    // ─────────────────────────────────────────────────────────────────────────────
    private async Task CreateOrUpdateEventAsync(AppDbContext ctx, ArticleCluster cluster, AgentExecution execution, CancellationToken ct)
    {
        if (cluster.Articles.Count == 0) return;

        var sectionId = cluster.Articles.First().Event.NewsSectionId;
        var titleWords = cluster.Title.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var cutoffDate = DateTime.UtcNow.AddDays(-3);

        var existingEvents = await ctx.NewsEvents
            .Where(e => e.EventType == "Detected" && e.IsActive
                     && e.NewsSectionId == sectionId
                     && e.CreatedAt >= cutoffDate)
            .ToListAsync(ct);

        var matched = existingEvents.FirstOrDefault(e =>
        {
            var existingWords = e.Title.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return titleWords.Intersect(existingWords).Count(w => w.Length > 4) >= 2;
        });

        NewsEvent newsEvent;
        if (matched != null)
        {
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

        foreach (var article in cluster.Articles)
            article.NewsEventId = newsEvent.Id;

        // Update cross-reference and source diversity metrics
        var allArticles = await ctx.NewsArticles
            .Where(a => a.NewsEventId == newsEvent.Id && !a.IsDeleted)
            .ToListAsync(ct);
        newsEvent.CrossReferenceCount = allArticles.Count;
        newsEvent.SourceDiversity = allArticles.Select(a => a.SourceName).Distinct().Count();

        await ctx.SaveChangesAsync(ct);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────────
    private void TrackCost(AppDbContext ctx, AgentExecution execution, AiProviderConfig? providerConfig)
    {
        // placeholder — cost tracked per-response in TrackCostFromResponse
    }

    private void TrackCostFromResponse(AppDbContext ctx, AgentExecution execution, AiResponse response)
    {
        if (!execution.AiProviderConfigId.HasValue || response.Cost <= 0) return;
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
            Title = "Noticias del día",
            Description = "Conjunto de noticias sin clasificar",
            Priority = EventPriority.Low,
            ImpactScore = 30,
            Category = "General",
            Articles = articles
        }];
    }

    private async Task GeocodeEventsMissingCoordsAsync(IServiceProvider services, AppDbContext ctx, AgentExecution execution, CancellationToken ct)
    {
        var missing = await ctx.NewsEvents
            .Where(e => e.Location != null && e.Latitude == null && e.IsActive)
            .Take(20).ToListAsync(ct);

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
                await Task.Delay(500, ct);
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
                catch { }
            }

            if (geocoded > 0)
            {
                await ctx.SaveChangesAsync(ct);
                await LogAsync(ctx, execution, AgentLogLevel.Info, $"Geocodificados {geocoded} eventos");
            }
        }
        catch (Exception ex)
        {
            await LogAsync(ctx, execution, AgentLogLevel.Warning, $"Error en geocodificación: {ex.Message}");
        }
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

record EventDetectionResult { public List<EventDto>? Events { get; init; } }

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
