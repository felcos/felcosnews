using ANews.Domain.Entities;
using ANews.Domain.Enums;
using ANews.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ANews.Infrastructure.Agents;

public abstract class BaseAgent : BackgroundService
{
    private readonly SemaphoreSlim _triggerNow = new(0, 1);
    protected readonly IServiceProvider _services;
    protected readonly ILogger _logger;
    protected abstract AgentType AgentType { get; }
    protected abstract string AgentName { get; }
    protected abstract TimeSpan Interval { get; }

    protected BaseAgent(IServiceProvider services, ILogger logger)
    {
        _services = services;
        _logger = logger;
    }

    public void TriggerNow()
    {
        // Libera el semáforo solo si no hay ya uno pendiente (máx 1)
        if (_triggerNow.CurrentCount == 0)
            _triggerNow.Release();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[{Agent}] Iniciado. Intervalo: {Interval}", AgentName, Interval);
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCycleWithTrackingAsync(stoppingToken);
            try
            {
                await _triggerNow.WaitAsync(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunCycleWithTrackingAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var execution = new AgentExecution
        {
            AgentType = AgentType,
            AgentName = AgentName,
            StartedAt = DateTime.UtcNow,
            Status = AgentStatus.Running,
            TriggerReason = "Scheduled"
        };
        ctx.AgentExecutions.Add(execution);
        await ctx.SaveChangesAsync(ct);

        try
        {
            await RunCycleAsync(scope.ServiceProvider, execution, ct);

            execution.Status = AgentStatus.Completed;
            execution.CompletedAt = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            execution.Status = AgentStatus.Cancelled;
            execution.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Agent}] Error en ciclo", AgentName);
            execution.Status = AgentStatus.Failed;
            execution.Error = ex.Message;
            execution.CompletedAt = DateTime.UtcNow;
        }
        finally
        {
            await ctx.SaveChangesAsync(ct);
        }
    }

    protected abstract Task RunCycleAsync(IServiceProvider services, AgentExecution execution, CancellationToken ct);

    protected async Task LogAsync(AppDbContext ctx, AgentExecution execution, AgentLogLevel level, string message, string? data = null)
    {
        ctx.AgentLogs.Add(new AgentLog
        {
            AgentExecutionId = execution.Id,
            Level = level,
            Message = message,
            Data = data,
            Timestamp = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        // Broadcast via SignalR (se suscribe desde el hub)
        OnLogEmitted?.Invoke(execution.Id, level, message);
    }

    public static event Action<int, AgentLogLevel, string>? OnLogEmitted;
}
