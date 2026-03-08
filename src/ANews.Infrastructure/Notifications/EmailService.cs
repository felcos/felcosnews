using ANews.Domain.Enums;
using ANews.Domain.Interfaces;
using MailKit.Net.Smtp;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;
using System.Text.Json;

namespace ANews.Infrastructure.Notifications;

public class EmailService : INotificationService
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string _password;
    private readonly string _from;
    private readonly ILogger<EmailService> _logger;

    public NotificationChannelType ChannelType => NotificationChannelType.Email;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _logger = logger;
        _host = config["Smtp:Host"] ?? "smtp.gmail.com";
        _port = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;
        _user = config["Smtp:User"] ?? "";
        _password = config["Smtp:Password"] ?? "";
        _from = config["Smtp:From"] ?? _user;
    }

    public async Task<bool> SendAsync(NotificationMessage message, CancellationToken ct = default)
    {
        try
        {
            var cfg = JsonSerializer.Deserialize<EmailChannelConfig>(message.ChannelConfig)
                ?? throw new InvalidOperationException("Config email inválida.");

            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_from));
            email.To.Add(MailboxAddress.Parse(cfg.Address));
            email.Subject = message.Title;

            var body = new BodyBuilder
            {
                HtmlBody = BuildHtmlBody(message),
                TextBody = $"{message.Title}\n\n{message.Body}" + (message.Url != null ? $"\n\nVer mas: {message.Url}" : "")
            };
            email.Body = body.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_host, _port, MailKit.Security.SecureSocketOptions.StartTls, ct);
            await smtp.AuthenticateAsync(_user, _password, ct);
            await smtp.SendAsync(email, ct);
            await smtp.DisconnectAsync(true, ct);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando email al usuario {UserId}", message.UserId);
            return false;
        }
    }

    public async Task<bool> VerifyChannelAsync(string config, string verificationToken)
    {
        try
        {
            var cfg = JsonSerializer.Deserialize<EmailChannelConfig>(config);
            if (cfg == null) return false;

            await SendAsync(new NotificationMessage
            {
                Title = "Verificacion AgenteNews",
                Body = $"Tu codigo de verificacion es: {verificationToken}\n\nIntroduce este codigo en el panel para verificar tu email.",
                ChannelConfig = config,
                UserId = 0
            });
            return true;
        }
        catch { return false; }
    }

    private static string BuildHtmlBody(NotificationMessage msg)
    {
        var link = msg.Url != null ? $"<p><a href=\"{msg.Url}\" style=\"background:#4a90e2;color:white;padding:10px 20px;text-decoration:none;border-radius:5px;\">Ver noticia</a></p>" : "";
        return $"""
            <!DOCTYPE html>
            <html>
            <body style="font-family:Arial,sans-serif;max-width:600px;margin:0 auto;padding:20px;">
              <div style="background:#1a1a2e;padding:20px;border-radius:8px;color:white;">
                <h2 style="color:#4a90e2;margin-top:0;">{System.Net.WebUtility.HtmlEncode(msg.Title)}</h2>
                <p style="color:#ccc;">{System.Net.WebUtility.HtmlEncode(msg.Body).Replace("\n", "<br>")}</p>
                {link}
                <hr style="border-color:#333;margin-top:20px;">
                <small style="color:#666;">AgenteNews - Noticias Inteligentes</small>
              </div>
            </body>
            </html>
            """;
    }
}

public record EmailChannelConfig
{
    public required string Address { get; init; }
}
