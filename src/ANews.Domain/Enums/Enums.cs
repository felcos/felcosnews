namespace ANews.Domain.Enums;

public enum EventPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum ArticleType
{
    Main,
    Analysis,
    Opinion,
    Reaction,
    FollowUp,
    Background,
    LiveUpdate
}

public enum EventTrend
{
    Stable,
    Increasing,
    Decreasing,
    Surging,
    Declining
}

public enum AiProviderType
{
    Claude,
    OpenAI,
    Kimi,
    Gemini,
    Groq,
    Custom
}

public enum AgentType
{
    NewsScanner,
    EventDetector,
    NewsClassifier,
    TrendAnalyzer,
    AlertGenerator,
    NotificationDispatcher,
    CostAggregator,
    ArticleSummarizer,
    DigestSender
}

public enum AgentStatus
{
    Idle,
    Running,
    Completed,
    Failed,
    Cancelled
}

public enum AgentLogLevel
{
    Debug,
    Info,
    Warning,
    Error
}

public enum NotificationChannelType
{
    Email,
    Telegram,
    WhatsApp,
    Discord,
    Webhook,
    BrowserPush
}

public enum NotificationFrequency
{
    Instant,
    Hourly,
    Daily,
    Weekly
}

public enum UserRole
{
    SuperAdmin,
    Admin,
    Analyst,
    User,
    ApiUser
}

public enum NewsSourceType
{
    Rss,
    Api,
    Scraper
}

public enum DigestionFrequency
{
    Daily,
    Weekly,
    Monthly
}
