using ANews.Domain.Enums;

namespace ANews.Domain.Interfaces;

public interface IAiProvider
{
    AiProviderType ProviderType { get; }
    string ProviderName { get; }
    string CurrentModel { get; }

    Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default);
    Task<List<float>> GetEmbeddingAsync(string text, CancellationToken ct = default);
    Task<bool> TestConnectionAsync();
}

public record AiRequest
{
    public required string SystemPrompt { get; init; }
    public required string UserPrompt { get; init; }
    public int MaxTokens { get; init; } = 1000;
    public double Temperature { get; init; } = 0.3;
    public string? Model { get; init; }
    public string? OperationTag { get; init; }
}

public record AiResponse
{
    public required string Content { get; init; }
    public int InputTokens { get; init; }
    public int OutputTokens { get; init; }
    public decimal Cost { get; init; }
    public required string Model { get; init; }
    public TimeSpan Duration { get; init; }
    public bool Success { get; init; } = true;
    public string? Error { get; init; }
}
