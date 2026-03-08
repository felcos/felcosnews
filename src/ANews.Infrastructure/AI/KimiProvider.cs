using ANews.Domain.Enums;
using ANews.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ANews.Infrastructure.AI;

// Kimi (Moonshot AI) uses OpenAI-compatible API
public class KimiProvider : OpenAiProvider
{
    public KimiProvider(string apiKey, string model, string baseUrl, ILogger logger)
        : base(apiKey, model, logger, baseUrl) { }

    public new AiProviderType ProviderType => AiProviderType.Kimi;
    public new string ProviderName => "Kimi (Moonshot AI)";
}
