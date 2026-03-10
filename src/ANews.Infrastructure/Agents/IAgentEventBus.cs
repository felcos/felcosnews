using ANews.Domain.Enums;

namespace ANews.Infrastructure.Agents;

/// <summary>
/// Replaces static events on BaseAgent to avoid memory leaks and enable testability.
/// </summary>
public interface IAgentEventBus
{
    event Action<int, AgentLogLevel, string>? OnLogEmitted;
    event Action<string, string>? OnExecutionCompleted;

    void EmitLog(int executionId, AgentLogLevel level, string message);
    void EmitExecutionCompleted(string agentName, string status);
}

public class AgentEventBus : IAgentEventBus
{
    public event Action<int, AgentLogLevel, string>? OnLogEmitted;
    public event Action<string, string>? OnExecutionCompleted;

    public void EmitLog(int executionId, AgentLogLevel level, string message)
        => OnLogEmitted?.Invoke(executionId, level, message);

    public void EmitExecutionCompleted(string agentName, string status)
        => OnExecutionCompleted?.Invoke(agentName, status);
}
