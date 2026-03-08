using System.Diagnostics;
using System.Text;
using System.Text.Json;
using ANews.Domain.Enums;
using ANews.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ANews.Infrastructure.AI;

public class OpenAiProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly ILogger _logger;

    public AiProviderType ProviderType => AiProviderType.OpenAI;
    public string ProviderName => "OpenAI";
    public string CurrentModel => _model;

    public OpenAiProvider(string apiKey, string model, ILogger logger, string? baseUrl = null)
    {
        _model = model;
        _logger = logger;
        // Ensure base URL ends with '/' so relative paths resolve correctly
        var url = (baseUrl ?? "https://api.openai.com/v1").TrimEnd('/') + "/";
        _http = new HttpClient { BaseAddress = new Uri(url) };
        _http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var body = new
            {
                model = request.Model ?? _model,
                max_tokens = request.MaxTokens,
                temperature = request.Temperature,
                messages = new[]
                {
                    new { role = "system", content = request.SystemPrompt },
                    new { role = "user", content = request.UserPrompt }
                }
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync("chat/completions", content, ct);
            resp.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync(ct));
            var text = result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
            var inputTokens = result.GetProperty("usage").GetProperty("prompt_tokens").GetInt32();
            var outputTokens = result.GetProperty("usage").GetProperty("completion_tokens").GetInt32();

            sw.Stop();
            return new AiResponse
            {
                Content = text,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                Cost = CalculateCost(inputTokens, outputTokens, request.Model ?? _model),
                Model = request.Model ?? _model,
                Duration = sw.Elapsed,
                Success = true
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error en OpenAI/Compatible API");
            return new AiResponse { Content = "", Model = _model, Duration = sw.Elapsed, Success = false, Error = ex.Message };
        }
    }

    public async Task<List<float>> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        try
        {
            var body = new { model = "text-embedding-3-small", input = text };
            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync("embeddings", content, ct);
            resp.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync(ct));
            return result.GetProperty("data")[0].GetProperty("embedding")
                .EnumerateArray().Select(e => e.GetSingle()).ToList();
        }
        catch { return []; }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var resp = await CompleteAsync(new AiRequest
            {
                SystemPrompt = "Test assistant.",
                UserPrompt = "Say OK.",
                MaxTokens = 10
            });
            return resp.Success;
        }
        catch { return false; }
    }

    private static decimal CalculateCost(int inputTokens, int outputTokens, string model)
    {
        var (inputRate, outputRate) = model switch
        {
            var m when m.Contains("gpt-4o-mini") => (0.00015m, 0.0006m),
            var m when m.Contains("gpt-4o") => (0.005m, 0.015m),
            var m when m.Contains("gpt-4-turbo") => (0.01m, 0.03m),
            var m when m.Contains("gpt-3.5") => (0.0005m, 0.0015m),
            _ => (0.005m, 0.015m)
        };
        return (inputTokens * inputRate / 1000) + (outputTokens * outputRate / 1000);
    }
}
