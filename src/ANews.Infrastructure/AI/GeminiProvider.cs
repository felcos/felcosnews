using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ANews.Domain.Enums;
using ANews.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ANews.Infrastructure.AI;

public class GeminiProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly ILogger _logger;

    public AiProviderType ProviderType => AiProviderType.Gemini;
    public string ProviderName => "Google Gemini";
    public string CurrentModel => _model;

    public GeminiProvider(string apiKey, string model, ILogger logger)
    {
        _apiKey = apiKey;
        _model = model;
        _logger = logger;
        _http = new HttpClient { BaseAddress = new Uri("https://generativelanguage.googleapis.com") };
    }

    public async Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var modelId = request.Model ?? _model;
            var body = new
            {
                system_instruction = new { parts = new[] { new { text = request.SystemPrompt } } },
                contents = new[] { new { parts = new[] { new { text = request.UserPrompt } } } },
                generationConfig = new { maxOutputTokens = request.MaxTokens, temperature = request.Temperature }
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync(
                $"/v1beta/models/{modelId}:generateContent?key={_apiKey}", content, ct);
            resp.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync(ct));
            var text = result.GetProperty("candidates")[0].GetProperty("content")
                .GetProperty("parts")[0].GetProperty("text").GetString() ?? "";

            var inputTokens = 0;
            var outputTokens = 0;
            if (result.TryGetProperty("usageMetadata", out var usage))
            {
                if (usage.TryGetProperty("promptTokenCount", out var pt)) inputTokens = pt.GetInt32();
                if (usage.TryGetProperty("candidatesTokenCount", out var ct2)) outputTokens = ct2.GetInt32();
            }

            sw.Stop();
            return new AiResponse
            {
                Content = text,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                Cost = CalculateCost(inputTokens, outputTokens, modelId),
                Model = modelId,
                Duration = sw.Elapsed,
                Success = true
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error en Gemini API");
            return new AiResponse { Content = "", Model = _model, Duration = sw.Elapsed, Success = false, Error = ex.Message };
        }
    }

    public async Task<List<float>> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        await Task.CompletedTask;
        return [];
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var resp = await CompleteAsync(new AiRequest { SystemPrompt = "Test.", UserPrompt = "Say OK.", MaxTokens = 10 });
            return resp.Success;
        }
        catch { return false; }
    }

    private static decimal CalculateCost(int inputTokens, int outputTokens, string model)
    {
        var (inputRate, outputRate) = model switch
        {
            var m when m.Contains("gemini-1.5-pro") => (0.00125m, 0.005m),
            var m when m.Contains("gemini-1.5-flash") => (0.000075m, 0.0003m),
            var m when m.Contains("gemini-2.0") => (0.000075m, 0.0003m),
            _ => (0.00125m, 0.005m)
        };
        return (inputTokens * inputRate / 1000) + (outputTokens * outputRate / 1000);
    }
}
