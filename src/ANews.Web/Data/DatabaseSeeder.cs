using ANews.Domain.Entities;
using ANews.Domain.Enums;
using ANews.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ANews.Web.Data;

public static class DatabaseSeeder
{
    public static async Task InitializeAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            await ctx.Database.MigrateAsync();
            logger.LogInformation("Base de datos migrada correctamente");

            await SeedRolesAsync(roleManager);
            await SeedAdminAsync(userManager, config, logger);
            await SeedSectionsAsync(ctx, logger);
            await MigrateOldSectionsAsync(ctx, logger);
            await SeedRssSourcesAsync(ctx);
            await SeedAiPlaceholderAsync(ctx, logger);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error inicializando base de datos");
            throw;
        }
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole<int>> roleManager)
    {
        foreach (var role in Enum.GetNames<UserRole>())
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<int>(role));
        }
    }

    private static async Task SeedAdminAsync(
        UserManager<ApplicationUser> userManager, IConfiguration config, ILogger logger)
    {
        var adminEmail = config["Admin:Email"] ?? "admin@anews.local";
        if (await userManager.FindByEmailAsync(adminEmail) != null) return;

        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            DisplayName = "Super Admin",
            Role = UserRole.SuperAdmin,
            IsActive = true,
            EmailConfirmed = true
        };
        var result = await userManager.CreateAsync(admin, config["Admin:Password"] ?? "Admin@123456!");
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, nameof(UserRole.SuperAdmin));
            logger.LogInformation("Admin inicial creado: {Email}", adminEmail);
        }
    }

    // ── 13 secciones finales (periodismo profesional, sin solapamiento) ──────
    private static async Task SeedSectionsAsync(AppDbContext ctx, ILogger logger)
    {
        var sectionDefs = new[]
        {
            //  Name                  Slug              Description                                                                      Default System  Color      Icon                  Sort
            ("Internacional",        "mundo",           "Relaciones internacionales, diplomacia, geopolitica, conflictos entre paises, guerras, defensa", true, true, "#4a90e2", "fa-globe-americas",  1),
            ("Politica",             "politica",        "Gobiernos, elecciones, legislacion, partidos, escandalos politicos, politica nacional",         false, false, "#e74c3c", "fa-landmark",        2),
            ("Economia y Negocios",  "economia",        "Mercados, empresas, finanzas, empleo, startups, fusiones, mercado inmobiliario, banca, bolsa",  false, false, "#f39c12", "fa-chart-line",      3),
            ("Tecnologia",           "tecnologia",      "IA, software, hardware, innovacion, redes sociales, regulacion tech, telecomunicaciones",       false, false, "#1abc9c", "fa-microchip",       4),
            ("Ciencia y Salud",      "ciencia",         "Investigacion, descubrimientos, medicina, epidemias, espacio, farmaceutica, salud publica",     false, false, "#8e44ad", "fa-flask",           5),
            ("Sociedad",             "sociedad",        "Educacion, derechos humanos, inmigracion, demografia, religion, genero, movimientos sociales",  false, false, "#2980b9", "fa-users",           6),
            ("Seguridad y Defensa",  "seguridad",       "Terrorismo, crimen organizado, ciberseguridad, ejercitos, espionaje, armas, inteligencia",      false, false, "#e74c3c", "fa-shield-alt",      7),
            ("Justicia",             "justicia",        "Tribunales, crimenes, corrupcion, condenas, investigaciones judiciales, sentencias",            false, false, "#c0392b", "fa-gavel",           8),
            ("Medio Ambiente",       "medioambiente",   "Clima, desastres naturales, energia, sostenibilidad, biodiversidad, contaminacion",             false, false, "#2ecc71", "fa-leaf",            9),
            ("Cultura y Deportes",   "cultura",         "Arte, cine, musica, literatura, deportes, competiciones, premios, patrimonio, teatro",          false, false, "#9b59b6", "fa-palette",         10),
            ("Gente y Corazon",      "gente",           "Celebrities, prensa rosa, realeza, reality TV, farandula, bodas, escandalos de famosos",        false, false, "#e91e63", "fa-heart",           11),
            ("Opinion y Analisis",   "opinion",         "Editoriales, columnas de opinion, analisis de fondo, perspectivas, tribuna",                    false, false, "#7f8c8d", "fa-comment-dots",    12),
            ("Ultima Hora",          "ultimahora",      "Breaking news, eventos en desarrollo, emergencias activas, alertas en directo",                 false, false, "#ff0040", "fa-bolt",            13),
        };

        foreach (var (name, slug, desc, isDefault, isSystem, color, icon, sort) in sectionDefs)
        {
            var existing = await ctx.NewsSections.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.Slug == slug);
            if (existing == null)
            {
                ctx.NewsSections.Add(new NewsSection
                {
                    Name = name, Slug = slug, Description = desc,
                    IsDefault = isDefault, IsSystemSection = isSystem,
                    Color = color, IconClass = icon, SortOrder = sort,
                    IsPublic = true
                });
            }
            else
            {
                existing.Name = name; existing.Description = desc;
                existing.Color = color; existing.IconClass = icon;
                existing.SortOrder = sort;
                existing.IsDeleted = false;
                ctx.NewsSections.Update(existing);
            }
        }
        await ctx.SaveChangesAsync();
    }

    // ── Migrar secciones antiguas a las nuevas ──────────────────────────────
    private static readonly Dictionary<string, string> SectionMigrationMap = new()
    {
        ["geopolitica"]     = "mundo",        // Geopolitica → Internacional
        ["conflictos"]      = "mundo",        // Conflictos → Internacional
        ["inteligencia"]    = "seguridad",    // Inteligencia → Seguridad y Defensa
        ["nbq"]             = "seguridad",    // NBQ & Armas → Seguridad y Defensa
        ["ciberseguridad"]  = "seguridad",    // Ciberseguridad → Seguridad y Defensa
        ["ciberguerra"]     = "seguridad",    // Ciberguerra (legacy) → Seguridad y Defensa
        ["negocios"]        = "economia",     // Negocios → Economia y Negocios
        ["salud"]           = "ciencia",      // Salud → Ciencia y Salud
        ["deportes"]        = "cultura",      // Deportes → Cultura y Deportes
        ["entretenimiento"] = "gente",        // Entretenimiento → Gente y Corazon
        ["terrorismo"]      = "seguridad",    // Terrorismo → Seguridad y Defensa
        ["tecnologia-dual"] = "seguridad",    // Tecnologia Dual → Seguridad y Defensa
        ["social"]          = "sociedad",     // Social → Sociedad
        ["farandula"]       = "gente",        // Farandula → Gente y Corazon
    };

    private static async Task MigrateOldSectionsAsync(AppDbContext ctx, ILogger logger)
    {
        var allSections = await ctx.NewsSections.IgnoreQueryFilters().ToListAsync();
        var sectionBySlug = allSections.ToDictionary(s => s.Slug, s => s);

        foreach (var (oldSlug, newSlug) in SectionMigrationMap)
        {
            if (!sectionBySlug.TryGetValue(oldSlug, out var oldSection)) continue;
            if (!sectionBySlug.TryGetValue(newSlug, out var newSection)) continue;
            if (oldSection.Id == newSection.Id) continue;

            // Move events from old section to new
            var events = await ctx.NewsEvents
                .IgnoreQueryFilters()
                .Where(e => e.NewsSectionId == oldSection.Id)
                .ToListAsync();

            if (events.Count > 0)
            {
                foreach (var ev in events)
                    ev.NewsSectionId = newSection.Id;

                logger.LogInformation("Migrados {Count} eventos de '{Old}' a '{New}'",
                    events.Count, oldSlug, newSlug);
            }

            // Move sources from old section to new
            var sources = await ctx.NewsSources
                .IgnoreQueryFilters()
                .Where(s => s.NewsSectionId == oldSection.Id)
                .ToListAsync();

            if (sources.Count > 0)
            {
                foreach (var src in sources)
                    src.NewsSectionId = newSection.Id;

                logger.LogInformation("Migradas {Count} fuentes de '{Old}' a '{New}'",
                    sources.Count, oldSlug, newSlug);
            }

            // Soft-delete the old section
            oldSection.IsDeleted = true;
            oldSection.IsPublic = false;
        }

        await ctx.SaveChangesAsync();
    }

    // ── Fuentes RSS con slugs actualizados ──────────────────────────────────
    private static async Task SeedRssSourcesAsync(AppDbContext ctx)
    {
        var sectionIds = await ctx.NewsSections
            .Where(s => !s.IsDeleted)
            .ToDictionaryAsync(s => s.Slug, s => s.Id);

        var rssSources = new (string name, string url, string slug, int credibility, string lang)[]
        {
            // ── Internacional ─────────────────────────────────────────────────
            ("Reuters World",              "https://feeds.reuters.com/reuters/worldNews",                                                         "mundo", 95, "en"),
            ("BBC News World",             "http://feeds.bbci.co.uk/news/world/rss.xml",                                                         "mundo", 95, "en"),
            ("El Pais Internacional",      "https://feeds.elpais.com/mrss-s/pages/ep/site/elpais.com/section/internacional/portada",              "mundo", 90, "es"),
            ("France 24 ES",               "https://www.france24.com/es/rss",                                                                    "mundo", 88, "es"),
            ("Foreign Policy",             "https://foreignpolicy.com/feed/",                                                                    "mundo", 92, "en"),
            ("Al Jazeera English",         "https://www.aljazeera.com/xml/rss/all.xml",                                                          "mundo", 88, "en"),
            ("DW Espanol",                 "https://rss.dw.com/xml/rss-sp-all",                                                                  "mundo", 87, "es"),
            ("EFE Internacional",          "https://www.efe.com/efe/espana/mundo/rss",                                                           "mundo", 90, "es"),
            ("RTVE Noticias",              "https://www.rtve.es/api/noticias.rss",                                                               "mundo", 88, "es"),
            ("Euronews ES",               "https://es.euronews.com/rss",                                                                         "mundo", 85, "es"),

            // ── Politica (enfoque Espana) ─────────────────────────────────────
            ("El Pais Espana",             "https://feeds.elpais.com/mrss-s/pages/ep/site/elpais.com/section/espana/portada",                     "politica", 90, "es"),
            ("El Mundo Espana",            "https://e00-elmundo.uecdn.es/elmundo/rss/espana.xml",                                                "politica", 85, "es"),
            ("La Vanguardia Politica",     "https://www.lavanguardia.com/rss/politica.xml",                                                      "politica", 87, "es"),
            ("ABC Espana",                 "https://www.abc.es/rss/feeds/abc_EspsanaEspworlds.xml",                                              "politica", 84, "es"),
            ("20 Minutos",                 "https://www.20minutos.es/rss/nacional/",                                                             "politica", 82, "es"),
            ("Publico",                    "https://www.publico.es/rss/politica",                                                                "politica", 80, "es"),
            ("elDiario.es",                "https://www.eldiario.es/rss/",                                                                       "politica", 83, "es"),
            ("Europa Press Espana",        "https://www.europapress.es/rss/rss.aspx?ch=00285",                                                   "politica", 88, "es"),
            ("Politico EU",                "https://www.politico.eu/feed/",                                                                      "politica", 90, "en"),

            // ── Economia y Negocios ───────────────────────────────────────────
            ("Reuters Business",           "https://feeds.reuters.com/reuters/businessNews",                                                     "economia", 93, "en"),
            ("Financial Times",            "https://www.ft.com/rss/home",                                                                       "economia", 94, "en"),
            ("El Economista",              "https://www.eleconomista.es/rss/rss-seleccion-ee.php",                                               "economia", 85, "es"),
            ("Cinco Dias",                 "https://cincodias.elpais.com/rss/cincodias/portada.xml",                                             "economia", 87, "es"),
            ("Expansion",                  "https://e00-expansion.uecdn.es/rss/portada.xml",                                                     "economia", 86, "es"),
            ("Bloomberg",                  "https://feeds.bloomberg.com/markets/news.rss",                                                       "economia", 94, "en"),
            ("CNBC",                       "https://search.cnbc.com/rs/search/combinedcms/view.xml?partnerId=wrss01&id=100003114",                "economia", 90, "en"),
            ("Europa Press Economia",      "https://www.europapress.es/rss/rss.aspx?ch=00296",                                                   "economia", 86, "es"),
            ("Bolsamania",                 "https://www.bolsamania.com/rss/",                                                                    "economia", 80, "es"),

            // ── Tecnologia ────────────────────────────────────────────────────
            ("Ars Technica",               "https://feeds.arstechnica.com/arstechnica/index",                                                    "tecnologia", 90, "en"),
            ("The Verge",                  "https://www.theverge.com/rss/index.xml",                                                             "tecnologia", 88, "en"),
            ("Wired",                      "https://www.wired.com/feed/rss",                                                                     "tecnologia", 89, "en"),
            ("Xataka",                     "https://www.xataka.com/feedburner.xml",                                                              "tecnologia", 85, "es"),
            ("Genbeta",                    "https://www.genbeta.com/feedburner.xml",                                                             "tecnologia", 82, "es"),
            ("TechCrunch",                 "https://techcrunch.com/feed/",                                                                       "tecnologia", 88, "en"),

            // ── Ciencia y Salud ───────────────────────────────────────────────
            ("Nature News",                "https://www.nature.com/nature.rss",                                                                  "ciencia", 95, "en"),
            ("Science News",               "https://www.science.org/rss/news_current.xml",                                                       "ciencia", 95, "en"),
            ("El Pais Ciencia",            "https://feeds.elpais.com/mrss-s/pages/ep/site/elpais.com/section/ciencia/portada",                    "ciencia", 88, "es"),
            ("Agencia SINC",               "https://www.agenciasinc.es/feed",                                                                    "ciencia", 87, "es"),
            ("WHO News",                   "https://www.who.int/rss-feeds/news-english.xml",                                                     "ciencia", 92, "en"),

            // ── Sociedad ──────────────────────────────────────────────────────
            ("El Pais Sociedad",           "https://feeds.elpais.com/mrss-s/pages/ep/site/elpais.com/section/sociedad/portada",                   "sociedad", 88, "es"),
            ("La Vanguardia Vida",         "https://www.lavanguardia.com/rss/vida.xml",                                                          "sociedad", 85, "es"),
            ("20 Minutos Sociedad",        "https://www.20minutos.es/rss/sociedad/",                                                             "sociedad", 80, "es"),
            ("BBC News Education",         "https://feeds.bbci.co.uk/news/education/rss.xml",                                                    "sociedad", 88, "en"),

            // ── Seguridad y Defensa ───────────────────────────────────────────
            ("War on the Rocks",           "https://warontherocks.com/feed/",                                                                    "seguridad", 90, "en"),
            ("Defense News",               "https://www.defensenews.com/arc/outboundfeeds/rss/",                                                 "seguridad", 88, "en"),
            ("Breaking Defense",           "https://breakingdefense.com/feed/",                                                                  "seguridad", 87, "en"),
            ("The War Zone (The Drive)",   "https://www.thedrive.com/the-war-zone/rss",                                                         "seguridad", 89, "en"),
            ("Krebs on Security",          "https://krebsonsecurity.com/feed/",                                                                  "seguridad", 95, "en"),
            ("Bleeping Computer",          "https://www.bleepingcomputer.com/feed/",                                                             "seguridad", 88, "en"),
            ("The Hacker News",            "https://feeds.feedburner.com/TheHackersNews",                                                        "seguridad", 85, "en"),
            ("Schneier on Security",       "https://www.schneier.com/feed/atom/",                                                                "seguridad", 93, "en"),
            ("Bellingcat",                 "https://www.bellingcat.com/feed/",                                                                   "seguridad", 88, "en"),
            ("Just Security",              "https://www.justsecurity.org/feed/",                                                                 "seguridad", 87, "en"),
            ("Lawfare",                    "https://www.lawfaremedia.org/rss.xml",                                                               "seguridad", 89, "en"),
            ("Arms Control Association",   "https://www.armscontrol.org/rss.xml",                                                               "seguridad", 92, "en"),
            ("Bulletin of Atomic Scientists","https://thebulletin.org/feed/",                                                                    "seguridad", 93, "en"),
            ("Infodefensa",                "https://www.infodefensa.com/rss/news.xml",                                                           "seguridad", 84, "es"),

            // ── Justicia ──────────────────────────────────────────────────────
            ("El Pais Tribunales",         "https://feeds.elpais.com/mrss-s/pages/ep/site/elpais.com/section/espana/cat/tribunales/portada",      "justicia", 88, "es"),
            ("Confilegal",                 "https://confilegal.com/feed/",                                                                       "justicia", 82, "es"),
            ("Noticias Juridicas",         "https://noticias.juridicas.com/rss/actualidad.xml",                                                  "justicia", 80, "es"),

            // ── Medio Ambiente ────────────────────────────────────────────────
            ("El Pais Clima",              "https://feeds.elpais.com/mrss-s/pages/ep/site/elpais.com/section/clima-y-medio-ambiente/portada",     "medioambiente", 87, "es"),
            ("National Geographic ES",     "https://www.nationalgeographic.com.es/rss",                                                          "medioambiente", 88, "es"),
            ("Carbon Brief",               "https://www.carbonbrief.org/feed",                                                                   "medioambiente", 90, "en"),

            // ── Cultura y Deportes ────────────────────────────────────────────
            ("Marca",                      "https://e00-marca.uecdn.es/rss/portada.xml",                                                        "cultura", 82, "es"),
            ("AS Deportes",                "https://as.com/rss/tags/ultimas_noticias.xml",                                                       "cultura", 82, "es"),
            ("El Pais Cultura",            "https://feeds.elpais.com/mrss-s/pages/ep/site/elpais.com/section/cultura/portada",                    "cultura", 88, "es"),
            ("BBC Culture",                "https://feeds.bbci.co.uk/news/entertainment_and_arts/rss.xml",                                       "cultura", 87, "en"),
            ("Mundo Deportivo",            "https://www.mundodeportivo.com/rss/home.xml",                                                        "cultura", 80, "es"),

            // ── Gente y Corazon ───────────────────────────────────────────────
            ("Hola",                       "https://www.hola.com/rss/",                                                                          "gente", 75, "es"),
            ("Vanitatis",                  "https://www.vanitatis.elconfidencial.com/rss/",                                                      "gente", 73, "es"),
            ("Lecturas",                   "https://www.lecturas.com/rss.xml",                                                                   "gente", 72, "es"),
            ("Semana",                     "https://www.semana.es/rss/",                                                                         "gente", 72, "es"),
            ("Diez Minutos",               "https://www.diezminutos.es/rss/",                                                                   "gente", 71, "es"),
            ("Cosmopolitan ES",            "https://www.cosmopolitan.com/es/rss/",                                                               "gente", 70, "es"),
            ("People EN",                  "https://people.com/feed/",                                                                           "gente", 78, "en"),
            ("E! News",                    "https://www.eonline.com/syndication/feeds/rssfeeds/topstories.xml",                                   "gente", 75, "en"),

            // ── Opinion y Analisis ────────────────────────────────────────────
            ("El Pais Opinion",            "https://feeds.elpais.com/mrss-s/pages/ep/site/elpais.com/section/opinion/portada",                    "opinion", 85, "es"),
            ("El Mundo Opinion",           "https://e00-elmundo.uecdn.es/elmundo/rss/opinion.xml",                                               "opinion", 83, "es"),
            ("The Guardian Opinion",       "https://www.theguardian.com/uk/commentisfree/rss",                                                   "opinion", 88, "en"),
        };

        foreach (var (name, url, slug, credibility, lang) in rssSources)
        {
            if (!await ctx.NewsSources.AnyAsync(s => s.Url == url))
            {
                if (sectionIds.TryGetValue(slug, out var sectionId))
                {
                    ctx.NewsSources.Add(new NewsSource
                    {
                        Name = name, Url = url,
                        Type = NewsSourceType.Rss,
                        NewsSectionId = sectionId,
                        CredibilityScore = credibility,
                        Language = lang,
                        IsActive = true
                    });
                }
            }
        }
        await ctx.SaveChangesAsync();
    }

    private static async Task SeedAiPlaceholderAsync(AppDbContext ctx, ILogger logger)
    {
        if (await ctx.AiProviderConfigs.AnyAsync()) return;

        ctx.AiProviderConfigs.Add(new AiProviderConfig
        {
            Name = "Claude Sonnet (configurar API key)",
            Provider = AiProviderType.Claude,
            Model = "claude-sonnet-4-6",
            EncryptedApiKey = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("PLACEHOLDER_CONFIGURE_IN_ADMIN")),
            CostPerInputTokenK = 0.003m,
            CostPerOutputTokenK = 0.015m,
            MonthlyBudgetLimit = 20m,
            RateLimitPerMinute = 60,
            IsActive = false,
            IsDefault = false
        });
        await ctx.SaveChangesAsync();
        logger.LogInformation("Proveedor IA placeholder creado. Configura tu API key en /admin/ai");
    }
}
