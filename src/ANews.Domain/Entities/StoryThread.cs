using ANews.Domain.Enums;

namespace ANews.Domain.Entities;

public class StoryThread : BaseEntity
{
    public required string Title { get; set; }
    public string? Summary { get; set; }
    public string? WhyItMatters { get; set; }
    public string? Background { get; set; }
    public string? WhatToWatch { get; set; }
    public string[] KeyActors { get; set; } = [];
    public string[] Tags { get; set; } = [];
    public StoryStatus Status { get; set; } = StoryStatus.Developing;
    public EventPriority MaxPriority { get; set; } = EventPriority.Medium;
    public decimal MaxImpactScore { get; set; } = 50;
    public DateTime FirstEventDate { get; set; } = DateTime.UtcNow;
    public DateTime LastEventDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastBriefingAt { get; set; }
    public int EventCount { get; set; } = 0;
    public int TotalArticles { get; set; } = 0;
    public int? PrimarySectionId { get; set; }

    public NewsSection? PrimarySection { get; set; }
    public ICollection<NewsEvent> Events { get; set; } = [];
}
