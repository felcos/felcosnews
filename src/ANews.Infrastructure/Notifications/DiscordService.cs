using System.Net.Http.Json;
using System.Text.Json;
using ANews.Domain.Enums;
using ANews.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ANews.Infrastructure.Notifications;

public class DiscordService : INotificationService
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<DiscordService> _logger;

    public NotificationChannelType ChannelType => NotificationChannelType.Discord;

    public DiscordService(IHttpClientFactory httpFactory, ILogger<DiscordService> logger)
    {
        _httpFactory = httpFactory;
        _logger = logger;
    }

    public async Task<bool> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        try
        {
            var config = JsonSerializer.Deserialize<DiscordChannelConfig>(message.ChannelConfig);
            if (config == null || string.IsNullOrEmpty(config.WebhookUrl)) return false;

            var embed = new
            {
                title = message.Title,
                description = message.Body,
                color = 0x4A90E2,
                timestamp = DateTime.UtcNow.ToString("O"),
                footer = new { text = "AgenteNews" }
            };

            var payload = new
            {
                embeds = new[] { embed }
            };

            var http = _httpFactory.CreateClient();
            var resp = await http.PostAsJsonAsync(config.WebhookUrl, payload, ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Discord webhook returned {Status}", resp.StatusCode);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discord notification failed");
            return false;
        }
    }

    public Task<bool> VerifyChannelAsync(string config, string verificationToken) => Task.FromResult(true);

}

public record DiscordChannelConfig
{
    public string WebhookUrl { get; init; } = "";
}
