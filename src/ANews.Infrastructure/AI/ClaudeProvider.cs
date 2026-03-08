using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ANews.Domain.Enums;
using ANews.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ANews.Infrastructure.AI;

public class ClaudeProvider : IAiProvider
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly ILogger _logger;

    public AiProviderType ProviderType => AiProviderType.Claude;
    public string ProviderName => "Anthropic Claude";
    public string CurrentModel => _model;

    public ClaudeProvider(string apiKey, string model, ILogger logger)
    {
        _model = model;
        _logger = logger;
        _http = new HttpClient { BaseAddress = new Uri("https://api.anthropic.com") };
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
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
                system = request.SystemPrompt,
                messages = new[] { new { role = "user", content = request.UserPrompt } }
            };

            var json = JsonSerializer.Serialize(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = await _http.PostAsync("/v1/messages", content, ct);
            resp.EnsureSuccessStatusCode();

            var result = JsonSerializer.Deserialize<JsonElement>(await resp.Content.ReadAsStringAsync(ct));
            var text = result.GetProperty("content")[0].GetProperty("text").GetString() ?? "";
            var inputTokens = result.GetProperty("usage").GetProperty("input_tokens").GetInt32();
            var outputTokens = result.GetProperty("usage").GetProperty("output_tokens").GetInt32();

            sw.Stop();
            return new AiResponse
            {
                Content = text,
                InputTokens = inputTokens,
                OutputTokens = outputTokens,
                Cost = CalculateCost(inputTokens, outputTokens, _model),
                Model = request.Model ?? _model,
                Duration = sw.Elapsed,
                Success = true
            };
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex, "Error en Claude API");
            return new AiResponse
            {
                Content = "",
                Model = _model,
                Duration = sw.Elapsed,
                Success = false,
                Error = ex.Message
            };
        }
    }

    public async Task<List<float>> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        // Claude no tiene API de embeddings, usar voyage-ai o devolver lista vacía
        await Task.CompletedTask;
        return [];
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var resp = await CompleteAsync(new AiRequest
            {
                SystemPrompt = "You are a test assistant.",
                UserPrompt = "Say OK in one word.",
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
            var m when m.Contains("claude-opus-4") => (0.015m, 0.075m),
            var m when m.Contains("claude-sonnet-4") => (0.003m, 0.015m),
            var m when m.Contains("claude-haiku-4") => (0.00025m, 0.00125m),
            _ => (0.003m, 0.015m)
        };
        return (inputTokens * inputRate / 1000) + (outputTokens * outputRate / 1000);
    }
}
