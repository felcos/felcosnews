using ANews.Domain.Enums;
using ANews.Domain.Interfaces;
using ANews.Infrastructure.Data;
using ANews.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ANews.Infrastructure.AI;

public class AiProviderFactory
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AiProviderFactory> _logger;
    private readonly KeyEncryptionService _encryption;

    public AiProviderFactory(IServiceProvider services, ILogger<AiProviderFactory> logger, KeyEncryptionService encryption)
    {
        _services = services;
        _logger = logger;
        _encryption = encryption;
    }

    public async Task<IAiProvider> GetDefaultProviderAsync()
    {
        using var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var config = await ctx.AiProviderConfigs
            .Where(p => p.IsActive && p.IsDefault)
            .OrderBy(p => p.TotalCostToday)
            .FirstOrDefaultAsync()
            ?? await ctx.AiProviderConfigs
                .Where(p => p.IsActive)
                .FirstOrDefaultAsync();

        if (config == null)
            throw new InvalidOperationException("No hay proveedores de IA configurados y activos.");

        return CreateProvider(config.Provider, _encryption.Decrypt(config.EncryptedApiKey), config.Model, config.BaseUrl);
    }

    public async Task<IAiProvider> GetProviderAsync(int configId)
    {
        using var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var config = await ctx.AiProviderConfigs.FindAsync(configId)
            ?? throw new InvalidOperationException($"Proveedor de IA con Id {configId} no encontrado.");

        return CreateProvider(config.Provider, _encryption.Decrypt(config.EncryptedApiKey), config.Model, config.BaseUrl);
    }

    public IAiProvider CreateProvider(AiProviderType type, string apiKey, string model, string? baseUrl = null)
    {
        return type switch
        {
            AiProviderType.Claude => new ClaudeProvider(apiKey, model, _logger),
            AiProviderType.OpenAI => new OpenAiProvider(apiKey, model, _logger),
            AiProviderType.Kimi => new KimiProvider(apiKey, model, baseUrl ?? "https://api.moonshot.cn/v1", _logger),
            AiProviderType.Gemini => new GeminiProvider(apiKey, model, _logger),
            AiProviderType.Groq => new OpenAiProvider(apiKey, model, _logger, "https://api.groq.com/openai/v1"),
            AiProviderType.Custom => new OpenAiProvider(apiKey, model, _logger, baseUrl),
            _ => throw new NotSupportedException($"Proveedor {type} no soportado.")
        };
    }
}
