using ANews.Domain.Enums;
using ANews.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;

namespace ANews.Infrastructure.Notifications;

public class TelegramService : INotificationService
{
    private readonly ITelegramBotClient? _bot;
    private readonly ILogger<TelegramService> _logger;

    public NotificationChannelType ChannelType => NotificationChannelType.Telegram;

    public TelegramService(IConfiguration config, ILogger<TelegramService> logger)
    {
        _logger = logger;
        var token = config["Telegram:BotToken"];
        if (!string.IsNullOrEmpty(token))
            _bot = new TelegramBotClient(token);
        else
            _logger.LogWarning("Telegram:BotToken no configurado. Las notificaciones Telegram no funcionarán.");
    }

    public async Task<bool> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        if (_bot == null) { _logger.LogWarning("Telegram no configurado"); return false; }
        try
        {
            var cfg = JsonSerializer.Deserialize<TelegramChannelConfig>(message.ChannelConfig)
                ?? throw new InvalidOperationException("Config de Telegram inválida.");

            var text = BuildMessageText(message);
            await _bot.SendMessage(cfg.ChatId, text, parseMode: ParseMode.Html, cancellationToken: ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando notificación Telegram al usuario {UserId}", message.UserId);
            return false;
        }
    }

    public async Task<bool> VerifyChannelAsync(string config, string verificationToken)
    {
        if (_bot == null) return false;
        try
        {
            var cfg = JsonSerializer.Deserialize<TelegramChannelConfig>(config);
            if (cfg == null) return false;

            await _bot.SendMessage(cfg.ChatId,
                $"Verificacion AgenteNews: <code>{verificationToken}</code>",
                parseMode: ParseMode.Html);
            return true;
        }
        catch { return false; }
    }

    private static string BuildMessageText(NotificationMessage message)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"<b>{EscapeHtml(message.Title)}</b>");
        sb.AppendLine();
        sb.AppendLine(EscapeHtml(message.Body));
        if (message.Url != null)
            sb.AppendLine($"\n<a href=\"{message.Url}\">Ver mas</a>");
        return sb.ToString();
    }

    private static string EscapeHtml(string text)
        => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}

public record TelegramChannelConfig
{
    public required string ChatId { get; init; }
}
