using System.Text;
using ANews.Domain.Enums;
using ANews.Infrastructure;
using ANews.Infrastructure.Data;
using ANews.Web.Data;
using ANews.Web.Endpoints;
using ANews.Web.Hubs;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.SignalR;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
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

// SignalR con Redis backplane en produccion
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

// HTTP Context & Clients
builder.Services.AddHttpContextAccessor();
builder.Services.AddHttpClient("nominatim", c =>
{
    c.DefaultRequestHeaders.Add("User-Agent", "AgenteNews/1.0 (news aggregator; contact admin)");
    c.Timeout = TimeSpan.FromSeconds(10);
});
builder.Services.AddHttpClient("rss", c =>
{
    c.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (compatible; AgenteNews/1.0; +https://agente.news/bot)");
    c.DefaultRequestHeaders.Add("Accept", "application/rss+xml, application/atom+xml, application/xml, text/xml, */*");
    c.Timeout = TimeSpan.FromSeconds(20);
});

// Controllers (para API REST publica)
builder.Services.AddControllers();

// Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opts =>
{
    opts.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "AgenteNews Public API",
        Version = "v1",
        Description = "API pública de AgenteNews — eventos, secciones, hilos narrativos y briefings."
    });
});

// CORS
builder.Services.AddCors(opts =>
{
    opts.AddPolicy("PublicApi", policy =>
        policy.WithOrigins(builder.Configuration["AppUrl"] ?? "*")
              .AllowAnyMethod()
              .AllowAnyHeader());
    opts.AddPolicy("Widget", policy =>
        policy.AllowAnyOrigin()
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
await DatabaseSeeder.InitializeAsync(app);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Necesario cuando la app esta detras de un reverse proxy (nginx termina TLS)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
app.UseHttpsRedirection();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        // Versioned assets (?v=X) get long cache; others get no-cache
        if (ctx.Context.Request.Query.ContainsKey("v"))
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        else
            ctx.Context.Response.Headers.CacheControl = "no-cache";
    }
});

// Swagger
app.UseSwagger();
app.UseSwaggerUI(opts =>
{
    opts.SwaggerEndpoint("/swagger/v1/swagger.json", "AgenteNews API v1");
    opts.RoutePrefix = "swagger";
});

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

// Wire up real-time agent log broadcasting via DI event bus
AgentMonitorHub.RegisterHubContext(
    app.Services.GetRequiredService<IHubContext<AgentMonitorHub>>(),
    app.Services.GetRequiredService<ANews.Infrastructure.Agents.IAgentEventBus>());

// API Controllers
app.MapControllers();

// Endpoints (extracted)
app.MapAccountEndpoints();
app.MapAdminApiEndpoints();
app.MapPublicApiEndpoints();

// Blazor
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Health
app.MapHealthChecks("/health");

app.Run();
