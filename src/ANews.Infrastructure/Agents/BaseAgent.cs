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
    private readonly IAgentEventBus _eventBus;
    protected abstract AgentType AgentType { get; }
    protected abstract string AgentName { get; }
    protected abstract TimeSpan DefaultInterval { get; }

    private TimeSpan _interval;
    private bool _isEnabled = true;

    public TimeSpan CurrentInterval => _interval;
    public bool IsAgentEnabled => _isEnabled;

    public void UpdateInterval(int minutes)
    {
        _interval = TimeSpan.FromMinutes(minutes);
        _logger.LogInformation("[{Agent}] Intervalo actualizado a {Interval}", AgentName, _interval);
    }

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled;
        _logger.LogInformation("[{Agent}] {State}", AgentName, enabled ? "Habilitado" : "Deshabilitado");
    }

    protected BaseAgent(IServiceProvider services, ILogger logger)
    {
        _services = services;
        _logger = logger;
        _eventBus = services.GetRequiredService<IAgentEventBus>();
    }

    public void TriggerNow()
    {
        if (_triggerNow.CurrentCount == 0)
            _triggerNow.Release();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Load config from DB
        _interval = DefaultInterval;
        try
        {
            using var scope = _services.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cfg = await ctx.AgentConfigs.FirstOrDefaultAsync(c => c.AgentType == AgentName, stoppingToken);
            if (cfg != null)
            {
                _interval = TimeSpan.FromMinutes(cfg.IntervalMinutes);
                _isEnabled = cfg.IsEnabled;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[{Agent}] No se pudo cargar config de DB, usando defaults", AgentName);
        }

        _logger.LogInformation("[{Agent}] Iniciado. Intervalo: {Interval}{Disabled}",
            AgentName, _interval, _isEnabled ? "" : " [DESHABILITADO]");

        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            if (_isEnabled)
                await RunCycleWithTrackingAsync(stoppingToken);

            try
            {
                await _triggerNow.WaitAsync(_interval, stoppingToken);
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
            execution.Error = $"{ex.GetType().Name}: {ex.Message}";
            execution.CompletedAt = DateTime.UtcNow;
        }
        finally
        {
            await ctx.SaveChangesAsync(ct);
            _eventBus.EmitExecutionCompleted(AgentName, execution.Status.ToString());
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

        _eventBus.EmitLog(execution.Id, level, message);
    }

}
