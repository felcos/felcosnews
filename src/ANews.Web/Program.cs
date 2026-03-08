using System.Text;
using ANews.Domain.Enums;
using ANews.Infrastructure;
using ANews.Infrastructure.Data;
using ANews.Web.Hubs;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Prevent unhandled async exceptions from crashing the process
AppDomain.CurrentDomain.UnhandledException += (_, e) =>
{
    Console.Error.WriteLine($"[FATAL] UnhandledException: {e.ExceptionObject}");
};
TaskScheduler.UnobservedTaskException += (_, e) =>
{
    e.SetObserved();
    Console.Error.WriteLine($"[WARN] UnobservedTaskException: {e.Exception?.Message}");
};

// Serilog
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

// Infrastructure (DB, Redis, AI, Notifications, Agents)
builder.Services.AddInfrastructure(builder.Configuration);

// Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole<int>>(opts =>
{
    opts.Password.RequiredLength = 8;
    opts.Password.RequireNonAlphanumeric = true;
    opts.User.RequireUniqueEmail = true;
    opts.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

// JWT Auth (para API tokens externos)
var jwtKey = builder.Configuration["Jwt:SecretKey"]
    ?? throw new InvalidOperationException("Jwt:SecretKey no configurado");

builder.Services.AddAuthentication(opts =>
{
    opts.DefaultScheme = IdentityConstants.ApplicationScheme;
    opts.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
})
.AddJwtBearer("ApiKey", opts =>
{
    opts.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization(opts =>
{
    opts.AddPolicy("RequireAdmin", p => p.RequireRole(nameof(UserRole.SuperAdmin), nameof(UserRole.Admin)));
    opts.AddPolicy("RequireSuperAdmin", p => p.RequireRole(nameof(UserRole.SuperAdmin)));
    opts.AddPolicy("RequireUser", p => p.RequireAuthenticatedUser());
});

// UI Services
builder.Services.AddScoped<ANews.Web.Services.ToastService>();

// Blazor Server
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(opts =>
{
    opts.DetailedErrors = builder.Environment.IsDevelopment();
    opts.DisconnectedCircuitMaxRetained = 50;
    opts.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(3);
    opts.MaxBufferedUnacknowledgedRenderBatches = 10;
});

// SignalR con Redis backplane en producción
var redisConn = builder.Configuration.GetConnectionString("Redis");
if (!builder.Environment.IsDevelopment() && !string.IsNullOrEmpty(redisConn))
{
    builder.Services.AddSignalR()
        .AddStackExchangeRedis(redisConn, opts =>
        {
            opts.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("anews");
        });
}
else
{
    builder.Services.AddSignalR();
}

// Health checks
builder.Services.AddHealthChecks()
    .AddNpgSql(builder.Configuration.GetConnectionString("Postgres") ?? "")
    .AddRedis(redisConn ?? "");

// HTTP Context
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("nominatim", c =>
{
    c.DefaultRequestHeaders.Add("User-Agent", "AgenteNews/1.0 (news aggregator; contact admin)");
    c.Timeout = TimeSpan.FromSeconds(10);
});

// Controllers (para API REST pública)
builder.Services.AddControllers();

// CORS
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("PublicApi", policy =>
        policy.WithOrigins(builder.Configuration["AppUrl"] ?? "*")
              .AllowAnyMethod()
              .AllowAnyHeader());
});

// Rate limiting
builder.Services.AddRateLimiter(opts =>
{
    opts.AddPolicy("api", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = 60 }));
    opts.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { Window = TimeSpan.FromMinutes(1), PermitLimit = 10 }));
});

var app = builder.Build();

// Migrations + seed
await InitializeDatabaseAsync(app);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Necesario cuando la app está detrás de un reverse proxy (nginx termina TLS)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSerilogRequestLogging();
app.UseCors("PublicApi");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// SignalR Hubs
app.MapHub<NewsHub>("/newshub");
app.MapHub<AdminHub>("/adminhub");
app.MapHub<AgentMonitorHub>("/agenthub");

// Wire up real-time agent log broadcasting
AgentMonitorHub.RegisterHubContext(
    app.Services.GetRequiredService<IHubContext<AgentMonitorHub>>());

// API Controllers
app.MapControllers();

// Blazor
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Health
app.MapHealthChecks("/health");

// Health detallado con estado de agentes
app.MapGet("/health/detail", async (AppDbContext db) =>
{
    var now = DateTime.UtcNow;
    var last24h = now.AddHours(-24);
    var agentStats = await db.AgentExecutions
        .Where(e => e.StartedAt >= last24h)
        .GroupBy(e => e.AgentType)
        .Select(g => new {
            agent = g.Key.ToString(),
            runs = g.Count(),
            lastRun = g.Max(e => e.StartedAt),
            errors = g.Count(e => e.Status == ANews.Domain.Enums.AgentStatus.Failed),
            totalCost = g.Sum(e => e.AiCost)
        })
        .ToListAsync();
    var eventCount  = await db.NewsEvents.CountAsync(e => !e.IsDeleted && e.IsActive);
    var articleCount = await db.NewsArticles.CountAsync(a => !a.IsDeleted);
    return Results.Json(new { status = "ok", utc = now, events = eventCount, articles = articleCount, agents = agentStats });
}).AllowAnonymous();

// Login endpoint real (Blazor Server no puede establecer cookies desde el circuito SignalR)
app.MapPost("/account/login", async (HttpContext http, SignInManager<ApplicationUser> signInMgr) =>
{
    var form = await http.Request.ReadFormAsync();
    var email = form["email"].ToString();
    var password = form["password"].ToString();
    var rememberMe = form["rememberMe"] == "true";
    var returnUrl = form["returnUrl"].ToString();

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        return Results.Redirect("/login?error=invalid");

    var result = await signInMgr.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: true);
    if (result.Succeeded)
        return Results.Redirect(string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl);
    if (result.IsLockedOut)
        return Results.Redirect("/login?error=locked");
    return Results.Redirect("/login?error=invalid");
}).RequireRateLimiting("auth").DisableAntiforgery();

// Logout endpoint
app.MapGet("/account/logout", async (HttpContext http, SignInManager<ApplicationUser> signInMgr) =>
{
    await signInMgr.SignOutAsync();
    return Results.Redirect("/");
}).DisableAntiforgery();

// Register endpoint
app.MapPost("/account/register", async (
    HttpContext http,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInMgr,
    Microsoft.AspNetCore.Identity.IEmailSender<ApplicationUser> emailSender,
    IConfiguration config) =>
{
    var form = await http.Request.ReadFormAsync();
    var displayName = form["displayName"].ToString().Trim();
    var email = form["email"].ToString().Trim();
    var password = form["password"].ToString();
    var confirmPassword = form["confirmPassword"].ToString();

    if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        return Results.Redirect("/register?error=required");

    if (password != confirmPassword)
        return Results.Redirect("/register?error=mismatch");

    var smtpConfigured = !string.IsNullOrEmpty(config["Smtp:Host"]) && !string.IsNullOrEmpty(config["Smtp:User"]);

    var user = new ApplicationUser
    {
        UserName = email,
        Email = email,
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? email.Split('@')[0] : displayName,
        Role = ANews.Domain.Enums.UserRole.User,
        IsActive = true,
        EmailConfirmed = !smtpConfigured // Auto-confirm when SMTP not configured
    };

    var result = await userManager.CreateAsync(user, password);
    if (!result.Succeeded)
    {
        var error = Uri.EscapeDataString(result.Errors.First().Description);
        return Results.Redirect($"/register?error={error}");
    }

    await userManager.AddToRoleAsync(user, nameof(ANews.Domain.Enums.UserRole.User));

    if (smtpConfigured)
    {
        // Send confirmation email
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = Uri.EscapeDataString(token);
        var appUrl = config["AppUrl"] ?? "https://news.websoftware.es";
        var confirmLink = $"{appUrl}/account/confirm-email?userId={user.Id}&token={encodedToken}";
        await emailSender.SendConfirmationLinkAsync(user, email, confirmLink);
        return Results.Redirect("/register?success=check-email");
    }

    await signInMgr.SignInAsync(user, isPersistent: false);
    return Results.Redirect("/user");
}).DisableAntiforgery();

// Email confirmation endpoint
app.MapGet("/account/confirm-email", async (
    int userId,
    string token,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInMgr) =>
{
    var user = await userManager.FindByIdAsync(userId.ToString());
    if (user == null) return Results.Redirect("/?confirmed=error");

    var result = await userManager.ConfirmEmailAsync(user, Uri.UnescapeDataString(token));
    if (!result.Succeeded) return Results.Redirect("/?confirmed=error");

    await signInMgr.SignInAsync(user, isPersistent: false);
    return Results.Redirect("/?confirmed=1");
}).DisableAntiforgery();

// Forgot password
app.MapPost("/account/forgot-password", async (
    HttpContext http,
    UserManager<ApplicationUser> userManager,
    Microsoft.AspNetCore.Identity.IEmailSender<ApplicationUser> emailSender,
    IConfiguration config) =>
{
    var form = await http.Request.ReadFormAsync();
    var email = form["email"].ToString().Trim();

    if (string.IsNullOrWhiteSpace(email))
        return Results.Redirect("/forgot-password");

    var user = await userManager.FindByEmailAsync(email);
    // Always redirect to same page (don't reveal if email exists)
    if (user != null && await userManager.IsEmailConfirmedAsync(user))
    {
        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encodedToken = Uri.EscapeDataString(token);
        var appUrl = config["AppUrl"] ?? "https://news.websoftware.es";
        var resetLink = $"{appUrl}/reset-password?userId={user.Id}&token={encodedToken}";
        await emailSender.SendPasswordResetLinkAsync(user, email, resetLink);
    }

    return Results.Redirect("/forgot-password?sent=1");
}).DisableAntiforgery();

// Reset password
app.MapPost("/account/reset-password", async (
    HttpContext http,
    UserManager<ApplicationUser> userManager) =>
{
    var form = await http.Request.ReadFormAsync();
    var userIdStr = form["userId"].ToString();
    var token = Uri.UnescapeDataString(form["token"].ToString());
    var password = form["password"].ToString();
    var confirmPassword = form["confirmPassword"].ToString();

    var encodedToken = Uri.EscapeDataString(token);
    var redirectBase = $"/reset-password?userId={userIdStr}&token={encodedToken}";

    if (password != confirmPassword)
        return Results.Redirect($"{redirectBase}&error=mismatch");

    var user = await userManager.FindByIdAsync(userIdStr);
    if (user == null)
        return Results.Redirect($"{redirectBase}&error=invalid");

    var result = await userManager.ResetPasswordAsync(user, token, password);
    if (!result.Succeeded)
    {
        var error = Uri.EscapeDataString(result.Errors.First().Description);
        return Results.Redirect($"{redirectBase}&error={error}");
    }

    return Results.Redirect($"/reset-password?userId={userIdStr}&token={encodedToken}&success=1");
}).DisableAntiforgery();

// Trigger manual de agentes (admin)
app.MapPost("/api/admin/agents/{type}/trigger", (string type, IServiceProvider sp) =>
{
    var triggered = type.ToLower() switch
    {
        "newsscanner" => TriggerAgent<ANews.Infrastructure.Agents.NewsScannerAgent>(sp),
        "eventdetector" => TriggerAgent<ANews.Infrastructure.Agents.EventDetectorAgent>(sp),
        "alertgenerator" => TriggerAgent<ANews.Infrastructure.Agents.AlertGeneratorAgent>(sp),
        "notificationdispatcher" => TriggerAgent<ANews.Infrastructure.Agents.NotificationDispatcherAgent>(sp),
        "articlesummarizer" => TriggerAgent<ANews.Infrastructure.Agents.ArticleSummarizerAgent>(sp),
        "digestsender" => TriggerAgent<ANews.Infrastructure.Agents.DigestSenderAgent>(sp),
        _ => false
    };
    return triggered
        ? Results.Ok(new { message = "Agente activado", agent = type })
        : Results.NotFound(new { error = "Tipo de agente no encontrado" });
}).RequireAuthorization("RequireAdmin");

static bool TriggerAgent<T>(IServiceProvider sp) where T : ANews.Infrastructure.Agents.BaseAgent
{
    var agent = sp.GetService<T>();
    if (agent == null) return false;
    agent.TriggerNow();
    return true;
}

// Geocode events that have a Location string but no coordinates
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
            await Task.Delay(400); // Nominatim rate limit: 1 req/s
            var url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(ev.Location!)}&format=json&limit=1";
            var res = await client.GetAsync(url);
            if (!res.IsSuccessStatusCode) continue;

            var json = await res.Content.ReadAsStringAsync();
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var arr = doc.RootElement;
            if (arr.GetArrayLength() > 0)
            {
                var first = arr[0];
                ev.Latitude = double.TryParse(first.GetProperty("lat").GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lat) ? lat : null;
                ev.Longitude = double.TryParse(first.GetProperty("lon").GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var lon) ? lon : null;
                if (ev.Latitude.HasValue) updated++;
            }
        }
        catch (Exception ex) { log.LogWarning("Geocode failed for event {Id}: {Err}", ev.Id, ex.Message); }
    }

    await ctx.SaveChangesAsync();
    return Results.Ok(new { message = $"Geocodificados {updated} de {events.Count} eventos", updated });
}).RequireAuthorization("RequireAdmin");

// Keywords del usuario logueado para el universo
app.MapGet("/api/user/module-keywords", async (HttpContext http, AppDbContext ctx, UserManager<ApplicationUser> userMgr) =>
{
    var user = await userMgr.GetUserAsync(http.User);
    if (user == null) return Results.Ok(new string[0]);

    var keywords = await ctx.Set<ANews.Domain.Entities.ModuleKeyword>()
        .Where(k => k.Module != null && k.Module.UserId == user.Id && k.Module.IsActive && !k.Module.IsDeleted)
        .Select(k => k.Keyword)
        .Distinct()
        .ToListAsync();

    return Results.Ok(keywords);
}).RequireAuthorization();

// RSS feed por módulo de usuario (/api/rss/{token})
app.MapGet("/api/rss/{token}", async (string token, AppDbContext ctx) =>
{
    var module = await ctx.UserModules
        .Include(m => m.Keywords)
        .FirstOrDefaultAsync(m => m.RssFeedToken == token && m.IsActive && !m.IsDeleted);

    if (module == null)
        return Results.NotFound();

    var keywords = module.Keywords.Select(k => k.Keyword.ToLower()).ToList();

    var events = await ctx.NewsEvents
        .Where(e => e.IsActive && e.EventType != "Unclassified")
        .OrderByDescending(e => e.CreatedAt)
        .Take(50)
        .ToListAsync();

    if (keywords.Count > 0)
    {
        events = events.Where(e =>
        {
            var text = $"{e.Title} {e.Description} {string.Join(" ", e.Tags)}".ToLowerInvariant();
            return keywords.Any(kw => text.Contains(kw));
        }).ToList();
    }

    var appUrl = app.Configuration["AppUrl"] ?? "https://news.websoftware.es";
    var xml = new System.Text.StringBuilder();
    xml.AppendLine("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
    xml.AppendLine("<rss version=\"2.0\" xmlns:atom=\"http://www.w3.org/2005/Atom\">");
    xml.AppendLine("<channel>");
    xml.AppendLine($"<title>AgenteNews — {System.Net.WebUtility.HtmlEncode(module.Name)}</title>");
    xml.AppendLine($"<link>{appUrl}</link>");
    xml.AppendLine($"<description>{System.Net.WebUtility.HtmlEncode(module.Description ?? module.Name)}</description>");
    xml.AppendLine($"<language>es</language>");
    xml.AppendLine($"<atom:link href=\"{appUrl}/api/rss/{token}\" rel=\"self\" type=\"application/rss+xml\"/>");

    foreach (var ev in events)
    {
        xml.AppendLine("<item>");
        xml.AppendLine($"<title>{System.Net.WebUtility.HtmlEncode(ev.Title)}</title>");
        xml.AppendLine($"<link>{appUrl}/?event={ev.Id}</link>");
        xml.AppendLine($"<description>{System.Net.WebUtility.HtmlEncode(ev.Description ?? ev.Title)}</description>");
        xml.AppendLine($"<pubDate>{ev.CreatedAt:R}</pubDate>");
        xml.AppendLine($"<guid>{appUrl}/?event={ev.Id}</guid>");
        xml.AppendLine("</item>");
    }

    xml.AppendLine("</channel>");
    xml.AppendLine("</rss>");

    return Results.Content(xml.ToString(), "application/rss+xml; charset=utf-8");
});

app.Run();

// ---- Initialization ----
static async Task InitializeDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<int>>>();
    var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        await ctx.Database.MigrateAsync();
        logger.LogInformation("Base de datos migrada correctamente");

        // Seed roles
        foreach (var role in Enum.GetNames<UserRole>())
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<int>(role));
        }

        // Seed super admin
        var adminEmail = config["Admin:Email"] ?? "admin@anews.local";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
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

        // Seed secciones — upsert por Slug
        var sectionDefs = new[]
        {
            ("NBQ", "nbq", "Nuclear, Biológico, Químico", true, true, "#ff0040", "fa-radiation", 1),
            ("Geopolítica", "geopolitica", "Relaciones internacionales y bloques de poder", false, false, "#4a90e2", "fa-globe", 2),
            ("Conflictos Armados", "conflictos", "Guerras, operaciones militares y armamento", false, false, "#ff6b35", "fa-fighter-jet", 3),
            ("Ciberguerra", "ciberguerra", "Ataques, vulnerabilidades y defensa digital", false, false, "#00ff88", "fa-shield-alt", 4),
            ("Inteligencia", "inteligencia", "Servicios de inteligencia y contrainteligencia", false, false, "#9b59b6", "fa-user-secret", 5),
            ("Terrorismo", "terrorismo", "Grupos terroristas y contraterrorismo", false, false, "#e74c3c", "fa-exclamation-triangle", 6),
            ("Tecnología Dual", "tecnologia-dual", "Tecnología de uso civil y militar", false, false, "#1abc9c", "fa-microchip", 7),
            ("Economía Global", "economia", "Mercados, sanciones y guerra económica", false, false, "#f39c12", "fa-chart-line", 8),
        };

        foreach (var (name, slug, desc, isDefault, isSystem, color, icon, sort) in sectionDefs)
        {
            if (!await ctx.NewsSections.AnyAsync(s => s.Slug == slug))
            {
                ctx.NewsSections.Add(new ANews.Domain.Entities.NewsSection
                {
                    Name = name, Slug = slug, Description = desc,
                    IsDefault = isDefault, IsSystemSection = isSystem,
                    Color = color, IconClass = icon, SortOrder = sort,
                    IsPublic = true
                });
            }
        }
        await ctx.SaveChangesAsync();

        // Seed fuentes RSS — upsert por URL
        var sectionIds = await ctx.NewsSections
            .Where(s => !s.IsDeleted)
            .ToDictionaryAsync(s => s.Slug, s => s.Id);

        var rssSources = new (string name, string url, string slug, int credibility)[]
        {
            // Geopolítica
            ("Reuters World", "https://feeds.reuters.com/reuters/worldNews", "geopolitica", 95),
            ("BBC News World", "http://feeds.bbci.co.uk/news/world/rss.xml", "geopolitica", 95),
            ("El País Internacional", "https://feeds.elpais.com/mrss-s/pages/ep/site/elpais.com/section/internacional/portada", "geopolitica", 90),
            ("France 24 ES", "https://www.france24.com/es/rss", "geopolitica", 88),
            ("Foreign Policy", "https://foreignpolicy.com/feed/", "geopolitica", 92),
            // Conflictos
            ("War on the Rocks", "https://warontherocks.com/feed/", "conflictos", 90),
            ("Defense News", "https://www.defensenews.com/arc/outboundfeeds/rss/", "conflictos", 88),
            ("Breaking Defense", "https://breakingdefense.com/feed/", "conflictos", 87),
            ("The War Zone (The Drive)", "https://www.thedrive.com/the-war-zone/rss", "conflictos", 89),
            // Ciberguerra
            ("Krebs on Security", "https://krebsonsecurity.com/feed/", "ciberguerra", 95),
            ("Bleeping Computer", "https://www.bleepingcomputer.com/feed/", "ciberguerra", 88),
            ("The Hacker News", "https://feeds.feedburner.com/TheHackersNews", "ciberguerra", 85),
            ("Schneier on Security", "https://www.schneier.com/feed/atom/", "ciberguerra", 93),
            // Inteligencia
            ("Bellingcat", "https://www.bellingcat.com/feed/", "inteligencia", 88),
            ("Just Security", "https://www.justsecurity.org/feed/", "inteligencia", 87),
            ("Lawfare", "https://www.lawfaremedia.org/rss.xml", "inteligencia", 89),
            // NBQ
            ("Arms Control Association", "https://www.armscontrol.org/rss.xml", "nbq", 92),
            ("Bulletin of Atomic Scientists", "https://thebulletin.org/feed/", "nbq", 93),
            // Economía
            ("Reuters Business", "https://feeds.reuters.com/reuters/businessNews", "economia", 93),
            ("Financial Times", "https://www.ft.com/rss/home", "economia", 94),
        };

        foreach (var (name, url, slug, credibility) in rssSources)
        {
            if (!await ctx.NewsSources.AnyAsync(s => s.Url == url))
            {
                if (sectionIds.TryGetValue(slug, out var sectionId))
                {
                    ctx.NewsSources.Add(new ANews.Domain.Entities.NewsSource
                    {
                        Name = name, Url = url,
                        Type = ANews.Domain.Enums.NewsSourceType.Rss,
                        NewsSectionId = sectionId,
                        CredibilityScore = credibility,
                        Language = "en",
                        IsActive = true
                    });
                }
            }
        }
        await ctx.SaveChangesAsync();

        // Seed placeholder proveedor Claude (inactivo hasta configurar API key)
        if (!await ctx.AiProviderConfigs.AnyAsync())
        {
            ctx.AiProviderConfigs.Add(new ANews.Domain.Entities.AiProviderConfig
            {
                Name = "Claude Sonnet (configurar API key)",
                Provider = ANews.Domain.Enums.AiProviderType.Claude,
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
    catch (Exception ex)
    {
        logger.LogError(ex, "Error inicializando base de datos");
        throw;
    }
}
