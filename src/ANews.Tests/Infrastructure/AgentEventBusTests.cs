using ANews.Domain.Enums;
using ANews.Infrastructure.Agents;

namespace ANews.Tests.Infrastructure;

public class AgentEventBusTests
{
    [Fact]
    public void EmitLog_NotifiesSubscribers()
    {
        var bus = new AgentEventBus();
        int receivedId = 0;
        AgentLogLevel receivedLevel = default;
        string receivedMsg = "";

        bus.OnLogEmitted += (id, level, msg) =>
        {
            receivedId = id;
            receivedLevel = level;
            receivedMsg = msg;
        };

        bus.EmitLog(42, AgentLogLevel.Info, "Test message");

        Assert.Equal(42, receivedId);
        Assert.Equal(AgentLogLevel.Info, receivedLevel);
        Assert.Equal("Test message", receivedMsg);
    }

    [Fact]
    public void EmitExecutionCompleted_NotifiesSubscribers()
    {
        var bus = new AgentEventBus();
        string receivedAgent = "";
        string receivedStatus = "";

        bus.OnExecutionCompleted += (agent, status) =>
        {
            receivedAgent = agent;
            receivedStatus = status;
        };

        bus.EmitExecutionCompleted("NewsScannerAgent", "Completed");

        Assert.Equal("NewsScannerAgent", receivedAgent);
        Assert.Equal("Completed", receivedStatus);
    }

    [Fact]
    public void EmitLog_NoSubscribers_DoesNotThrow()
    {
        var bus = new AgentEventBus();
        var ex = Record.Exception(() => bus.EmitLog(1, AgentLogLevel.Error, "No subscribers"));
        Assert.Null(ex);
    }

    [Fact]
    public void MultipleSubscribers_AllNotified()
    {
        var bus = new AgentEventBus();
        int count = 0;

        bus.OnLogEmitted += (_, _, _) => count++;
        bus.OnLogEmitted += (_, _, _) => count++;
        bus.OnLogEmitted += (_, _, _) => count++;

        bus.EmitLog(1, AgentLogLevel.Info, "test");

        Assert.Equal(3, count);
    }
}
