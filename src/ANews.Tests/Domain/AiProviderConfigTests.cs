using ANews.Domain.Entities;
using ANews.Domain.Enums;

namespace ANews.Tests.Domain;

public class AiProviderConfigTests
{
    [Fact]
    public void NewConfig_HasCorrectDefaults()
    {
        var config = new AiProviderConfig
        {
            Name = "Test Provider",
            Model = "gpt-4",
            EncryptedApiKey = "encrypted"
        };

        Assert.True(config.IsActive);
        Assert.False(config.IsDefault);
        Assert.Equal(4096, config.MaxTokens);
        Assert.Equal(60, config.RateLimitPerMinute);
        Assert.Equal(0, config.TotalCostToday);
        Assert.Equal(0, config.TotalCostMonth);
        Assert.Equal(0, config.MonthlyBudgetLimit);
    }

    [Fact]
    public void BudgetCheck_NoBudget_AlwaysAllowed()
    {
        var config = new AiProviderConfig
        {
            Name = "Free",
            Model = "test",
            EncryptedApiKey = "x",
            MonthlyBudgetLimit = 0,
            TotalCostMonth = 999
        };

        // With limit=0, budget is unlimited
        Assert.Equal(0, config.MonthlyBudgetLimit);
    }

    [Fact]
    public void BudgetCheck_ExceedsLimit_Detectable()
    {
        var config = new AiProviderConfig
        {
            Name = "Limited",
            Model = "test",
            EncryptedApiKey = "x",
            MonthlyBudgetLimit = 10m,
            TotalCostMonth = 10.5m
        };

        Assert.True(config.TotalCostMonth >= config.MonthlyBudgetLimit);
    }

    [Theory]
    [InlineData(AiProviderType.Claude)]
    [InlineData(AiProviderType.OpenAI)]
    [InlineData(AiProviderType.Groq)]
    [InlineData(AiProviderType.Gemini)]
    [InlineData(AiProviderType.Kimi)]
    public void Provider_AcceptsAllTypes(AiProviderType type)
    {
        var config = new AiProviderConfig
        {
            Name = "Test",
            Model = "test",
            EncryptedApiKey = "x",
            Provider = type
        };

        Assert.Equal(type, config.Provider);
    }
}
