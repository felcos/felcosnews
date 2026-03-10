using ANews.Domain.Entities;
using ANews.Domain.Enums;
using ANews.Infrastructure.Agents;
using ANews.Infrastructure.AI;
using ANews.Infrastructure.Data;
using ANews.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ANews.Tests.Infrastructure;

public class AiProviderFactoryTests
{
    private static ServiceProvider CreateTestServiceProvider(string dbName)
    {
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(opts =>
            opts.UseInMemoryDatabase(dbName));

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AiKeys:EncryptionKey"] = "test-encryption-key-32-chars-long!"
            })
            .Build();

        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddSingleton<KeyEncryptionService>();
        services.AddSingleton<IAgentEventBus, AgentEventBus>();

        // Register the service provider itself so AiProviderFactory can create scopes
        var sp = services.BuildServiceProvider();
        return sp;
    }

    private static AiProviderFactory CreateFactory(ServiceProvider sp)
    {
        return new AiProviderFactory(
            sp,
            NullLogger<AiProviderFactory>.Instance,
            sp.GetRequiredService<KeyEncryptionService>());
    }

    [Fact]
    public async Task GetDefaultProvider_NoProviders_ThrowsInvalidOperation()
    {
        var sp = CreateTestServiceProvider($"AiTest_{Guid.NewGuid()}");
        var factory = CreateFactory(sp);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.GetDefaultProviderAsync());

        sp.Dispose();
    }

    [Fact]
    public async Task GetDefaultProvider_BudgetExceeded_ThrowsAndDisables()
    {
        var dbName = $"AiTest_{Guid.NewGuid()}";
        var sp = CreateTestServiceProvider(dbName);
        var encryption = sp.GetRequiredService<KeyEncryptionService>();

        // Seed data
        using (var scope = sp.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.AiProviderConfigs.Add(new AiProviderConfig
            {
                Name = "Over Budget",
                Provider = AiProviderType.OpenAI,
                Model = "gpt-4",
                EncryptedApiKey = encryption.Encrypt("sk-test"),
                IsActive = true,
                IsDefault = true,
                MonthlyBudgetLimit = 10m,
                TotalCostMonth = 15m
            });
            await ctx.SaveChangesAsync();
        }

        var factory = CreateFactory(sp);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => factory.GetDefaultProviderAsync());
        Assert.Contains("Presupuesto mensual agotado", ex.Message);

        // Verify provider was auto-disabled
        using (var scope = sp.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var provider = await ctx.AiProviderConfigs.FirstAsync();
            Assert.False(provider.IsActive);
        }

        sp.Dispose();
    }

    [Fact]
    public async Task GetDefaultProvider_WithinBudget_ReturnsProvider()
    {
        var dbName = $"AiTest_{Guid.NewGuid()}";
        var sp = CreateTestServiceProvider(dbName);
        var encryption = sp.GetRequiredService<KeyEncryptionService>();

        using (var scope = sp.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.AiProviderConfigs.Add(new AiProviderConfig
            {
                Name = "Within Budget",
                Provider = AiProviderType.OpenAI,
                Model = "gpt-4",
                EncryptedApiKey = encryption.Encrypt("sk-test"),
                BaseUrl = "https://api.openai.com/v1",
                IsActive = true,
                IsDefault = true,
                MonthlyBudgetLimit = 10m,
                TotalCostMonth = 5m
            });
            await ctx.SaveChangesAsync();
        }

        var factory = CreateFactory(sp);
        var provider = await factory.GetDefaultProviderAsync();
        Assert.NotNull(provider);

        sp.Dispose();
    }

    [Fact]
    public async Task GetDefaultProvider_UnlimitedBudget_AlwaysAllowed()
    {
        var dbName = $"AiTest_{Guid.NewGuid()}";
        var sp = CreateTestServiceProvider(dbName);
        var encryption = sp.GetRequiredService<KeyEncryptionService>();

        using (var scope = sp.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            ctx.AiProviderConfigs.Add(new AiProviderConfig
            {
                Name = "Unlimited",
                Provider = AiProviderType.OpenAI,
                Model = "gpt-4",
                EncryptedApiKey = encryption.Encrypt("sk-test"),
                BaseUrl = "https://api.openai.com/v1",
                IsActive = true,
                IsDefault = true,
                MonthlyBudgetLimit = 0,
                TotalCostMonth = 9999m
            });
            await ctx.SaveChangesAsync();
        }

        var factory = CreateFactory(sp);
        var provider = await factory.GetDefaultProviderAsync();
        Assert.NotNull(provider);

        sp.Dispose();
    }
}
