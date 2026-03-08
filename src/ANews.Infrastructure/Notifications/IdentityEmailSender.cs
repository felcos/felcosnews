using ANews.Infrastructure.Data;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MimeKit;

namespace ANews.Infrastructure.Notifications;

/// <summary>
/// ASP.NET Identity email sender. Used for email confirmation and password reset.
/// Requires Smtp:Host/Port/User/Password/From in config. Graceful if not configured.
/// </summary>
public class IdentityEmailSender : IEmailSender<ApplicationUser>
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _user;
    private readonly string _password;
    private readonly string _from;
    private readonly bool _configured;
    private readonly ILogger<IdentityEmailSender> _logger;

    public IdentityEmailSender(IConfiguration config, ILogger<IdentityEmailSender> logger)
    {
        _logger = logger;
        _host = config["Smtp:Host"] ?? "";
        _port = int.TryParse(config["Smtp:Port"], out var p) ? p : 587;
        _user = config["Smtp:User"] ?? "";
        _password = config["Smtp:Password"] ?? "";
        _from = config["Smtp:From"] ?? _user;
        _configured = !string.IsNullOrEmpty(_host) && !string.IsNullOrEmpty(_user) && !string.IsNullOrEmpty(_password);
    }

    public Task SendRawEmailAsync(string toEmail, string subject, string htmlBody) =>
        SendAsync(toEmail, subject, htmlBody);

    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendAsync(email, "Confirma tu cuenta — AgenteNews", BuildConfirmationHtml(user.DisplayName, confirmationLink));

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendAsync(email, "Restablecer contraseña — AgenteNews", BuildPasswordResetHtml(user.DisplayName, resetLink));

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendAsync(email, "Código de restablecimiento — AgenteNews", BuildCodeHtml(user.DisplayName, resetCode));

    private async Task SendAsync(string toEmail, string subject, string htmlBody)
    {
        if (!_configured)
        {
            _logger.LogWarning("SMTP no configurado — email a {Email} no enviado: {Subject}", toEmail, subject);
            return;
        }

        try
        {
            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_from));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_host, _port, MailKit.Security.SecureSocketOptions.StartTls);
            await smtp.AuthenticateAsync(_user, _password);
            await smtp.SendAsync(message);
            await smtp.DisconnectAsync(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando email a {Email}", toEmail);
        }
    }

    private static string BuildConfirmationHtml(string name, string link) => $"""
        <!DOCTYPE html><html><body style="font-family:Arial,sans-serif;background:#0a0e1a;padding:20px;">
        <div style="max-width:500px;margin:0 auto;background:#1a1f35;border-radius:12px;padding:30px;color:#e0e0e0;">
          <h2 style="color:#4a90e2;margin-top:0;"><i>AGENTE</i><strong>NEWS</strong></h2>
          <p>Hola <strong>{System.Net.WebUtility.HtmlEncode(name)}</strong>,</p>
          <p>Gracias por registrarte. Confirma tu cuenta haciendo clic en el siguiente enlace:</p>
          <p style="text-align:center;margin:30px 0;">
            <a href="{link}" style="background:#4a90e2;color:white;padding:14px 28px;text-decoration:none;border-radius:8px;font-weight:bold;display:inline-block;">
              Confirmar cuenta
            </a>
          </p>
          <p style="color:#888;font-size:12px;">Si no te has registrado, ignora este email. El enlace expira en 24 horas.</p>
          <hr style="border-color:#333;"><small style="color:#555;">AgenteNews — Noticias Inteligentes</small>
        </div></body></html>
        """;

    private static string BuildPasswordResetHtml(string name, string link) => $"""
        <!DOCTYPE html><html><body style="font-family:Arial,sans-serif;background:#0a0e1a;padding:20px;">
        <div style="max-width:500px;margin:0 auto;background:#1a1f35;border-radius:12px;padding:30px;color:#e0e0e0;">
          <h2 style="color:#4a90e2;margin-top:0;"><i>AGENTE</i><strong>NEWS</strong></h2>
          <p>Hola <strong>{System.Net.WebUtility.HtmlEncode(name)}</strong>,</p>
          <p>Recibimos una solicitud para restablecer tu contraseña:</p>
          <p style="text-align:center;margin:30px 0;">
            <a href="{link}" style="background:#4a90e2;color:white;padding:14px 28px;text-decoration:none;border-radius:8px;font-weight:bold;display:inline-block;">
              Restablecer contraseña
            </a>
          </p>
          <p style="color:#888;font-size:12px;">Si no solicitaste este cambio, ignora este email. El enlace expira en 1 hora.</p>
          <hr style="border-color:#333;"><small style="color:#555;">AgenteNews — Noticias Inteligentes</small>
        </div></body></html>
        """;

    private static string BuildCodeHtml(string name, string code) => $"""
        <!DOCTYPE html><html><body style="font-family:Arial,sans-serif;background:#0a0e1a;padding:20px;">
        <div style="max-width:500px;margin:0 auto;background:#1a1f35;border-radius:12px;padding:30px;color:#e0e0e0;">
          <h2 style="color:#4a90e2;margin-top:0;"><i>AGENTE</i><strong>NEWS</strong></h2>
          <p>Hola <strong>{System.Net.WebUtility.HtmlEncode(name)}</strong>,</p>
          <p>Tu código de restablecimiento es:</p>
          <p style="text-align:center;margin:30px 0;font-size:28px;letter-spacing:8px;font-weight:bold;color:#4a90e2;">{System.Net.WebUtility.HtmlEncode(code)}</p>
          <p style="color:#888;font-size:12px;">Si no solicitaste este cambio, ignora este email.</p>
          <hr style="border-color:#333;"><small style="color:#555;">AgenteNews — Noticias Inteligentes</small>
        </div></body></html>
        """;
}
