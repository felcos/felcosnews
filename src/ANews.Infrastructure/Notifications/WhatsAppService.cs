using ANews.Domain.Enums;
using ANews.Domain.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Twilio;
using Twilio.Rest.Api.V2010.Account;

namespace ANews.Infrastructure.Notifications;

public class WhatsAppService : INotificationService
{
    private readonly string _from;
    private readonly ILogger<WhatsAppService> _logger;

    public NotificationChannelType ChannelType => NotificationChannelType.WhatsApp;

    private readonly bool _configured;

    public WhatsAppService(IConfiguration config, ILogger<WhatsAppService> logger)
    {
        _logger = logger;
        var sid = config["Twilio:AccountSid"];
        var token = config["Twilio:AuthToken"];
        _from = config["Twilio:WhatsAppFrom"] ?? "whatsapp:+14155238886";

        if (!string.IsNullOrEmpty(sid) && !string.IsNullOrEmpty(token))
        {
            TwilioClient.Init(sid, token);
            _configured = true;
        }
        else
        {
            _logger.LogWarning("Twilio:AccountSid/AuthToken no configurados. Las notificaciones WhatsApp no funcionarán.");
        }
    }

    public async Task<bool> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        if (!_configured) { _logger.LogWarning("WhatsApp no configurado"); return false; }
        try
        {
            var cfg = JsonSerializer.Deserialize<WhatsAppChannelConfig>(message.ChannelConfig)
                ?? throw new InvalidOperationException("Config WhatsApp inválida.");

            var body = $"*{message.Title}*\n\n{message.Body}";
            if (message.Url != null)
                body += $"\n\nVer mas: {message.Url}";

            await MessageResource.CreateAsync(
                body: body,
                from: new Twilio.Types.PhoneNumber(_from),
                to: new Twilio.Types.PhoneNumber($"whatsapp:{cfg.PhoneNumber}"));

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando WhatsApp al usuario {UserId}", message.UserId);
            return false;
        }
    }

    public async Task<bool> VerifyChannelAsync(string config, string verificationToken)
    {
        if (!_configured) return false;
        try
        {
            var cfg = JsonSerializer.Deserialize<WhatsAppChannelConfig>(config);
            if (cfg == null) return false;

            await MessageResource.CreateAsync(
                body: $"AgenteNews - Tu codigo de verificacion es: {verificationToken}",
                from: new Twilio.Types.PhoneNumber(_from),
                to: new Twilio.Types.PhoneNumber($"whatsapp:{cfg.PhoneNumber}"));

            return true;
        }
        catch { return false; }
    }
}

public record WhatsAppChannelConfig
{
    public required string PhoneNumber { get; init; }
}
