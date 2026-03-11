using ANews.Domain.Enums;

namespace ANews.Domain.Entities;

public class EventBriefing : BaseEntity
{
    public int? NewsEventId { get; set; }
    public int? StoryThreadId { get; set; }
    public BriefingType Type { get; set; } = BriefingType.EventContext;
    public required string Title { get; set; }
    public string? WhyItMatters { get; set; }
    public string? Background { get; set; }
    public string? KeyActors { get; set; }
    public string? WhatToWatch { get; set; }
    public string? FullContent { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ExpiresAt { get; set; }
    public int SourceArticleCount { get; set; }

    public NewsEvent? Event { get; set; }
    public StoryThread? StoryThread { get; set; }
}
