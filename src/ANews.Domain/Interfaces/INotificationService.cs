using ANews.Domain.Enums;

namespace ANews.Domain.Interfaces;

public interface INotificationService
{
    NotificationChannelType ChannelType { get; }
    Task<bool> SendAsync(NotificationMessage message, CancellationToken ct = default);
    Task<bool> VerifyChannelAsync(string config, string verificationToken);
}

public record NotificationMessage
{
    public required string Title { get; init; }
    public required string Body { get; init; }
    public string? Url { get; init; }
    public string? ImageUrl { get; init; }
    public required string ChannelConfig { get; init; }
    public int UserId { get; init; }
    public int? NewsEventId { get; init; }
}
