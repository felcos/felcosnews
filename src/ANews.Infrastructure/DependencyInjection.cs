using ANews.Domain.Interfaces;
using ANews.Infrastructure.Agents;
using ANews.Infrastructure.AI;
using ANews.Infrastructure.Data;
using ANews.Infrastructure.Notifications;
using ANews.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ANews.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // Database
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(config.GetConnectionString("Postgres"));
        dataSourceBuilder.EnableDynamicJson();
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContext<AppDbContext>(opts =>
            opts.UseNpgsql(
                dataSource,
                o => o.MigrationsAssembly("ANews.Infrastructure")
                      .EnableRetryOnFailure(3)));

        // Redis
        services.AddStackExchangeRedisCache(opts =>
        {
            opts.Configuration = config.GetConnectionString("Redis");
        });

        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));

        // Security
        services.AddSingleton<ANews.Infrastructure.Security.KeyEncryptionService>();

        // AI Factory
        services.AddSingleton<AiProviderFactory>();

        // HTTP Client (required by DiscordService)
        services.AddHttpClient();

        // Notification Services
        services.AddSingleton<INotificationService, TelegramService>();
        services.AddSingleton<INotificationService, WhatsAppService>();
        services.AddSingleton<INotificationService, EmailService>();
        services.AddSingleton<INotificationService, DiscordService>();

        // Identity email sender (confirmation + password reset)
        services.AddTransient<Microsoft.AspNetCore.Identity.IEmailSender<Data.ApplicationUser>, Notifications.IdentityEmailSender>();

        // Background Agents — registrados como Singleton para poder resolverlos desde DI (TriggerNow)
        services.AddSingleton<NewsScannerAgent>();
        services.AddSingleton<EventDetectorAgent>();
        services.AddSingleton<AlertGeneratorAgent>();
        services.AddSingleton<NotificationDispatcherAgent>();
        services.AddSingleton<ArticleSummarizerAgent>();
        services.AddSingleton<DigestSenderAgent>();
        services.AddHostedService(sp => sp.GetRequiredService<NewsScannerAgent>());
        services.AddHostedService(sp => sp.GetRequiredService<EventDetectorAgent>());
        services.AddHostedService(sp => sp.GetRequiredService<AlertGeneratorAgent>());
        services.AddHostedService(sp => sp.GetRequiredService<NotificationDispatcherAgent>());
        services.AddHostedService(sp => sp.GetRequiredService<ArticleSummarizerAgent>());
        services.AddHostedService(sp => sp.GetRequiredService<DigestSenderAgent>());

        return services;
    }
}
