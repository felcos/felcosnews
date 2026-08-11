using ANews.Domain.Entities;
using ANews.Domain.Enums;
using ANews.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ANews.Infrastructure.Agents;

/// <summary>
/// Agente de limpieza mensual de la base de datos.
/// Elimina artículos de más de 30 días, audit logs de más de 90 días
/// y agent logs de más de 30 días para mantener el tamaño bajo control.
/// </summary>
public class DatabaseCleanupAgent : BaseAgent
{
    protected override AgentType AgentType => AgentType.DatabaseCleanup;
    protected override string AgentName => "DatabaseCleanupAgent";
    protected override TimeSpan DefaultInterval => TimeSpan.FromDays(30);

    public DatabaseCleanupAgent(IServiceProvider services, ILogger<DatabaseCleanupAgent> logger)
        : base(services, logger) { }

    protected override async Task RunCycleAsync(IServiceProvider services, AgentExecution execution, CancellationToken ct)
    {
        AppDbContext ctx = services.GetRequiredService<AppDbContext>();

        DateTime corteArticulos = DateTime.UtcNow.AddDays(-30);
        DateTime corteAuditLogs = DateTime.UtcNow.AddDays(-90);
        DateTime corteAgentLogs = DateTime.UtcNow.AddDays(-30);

        await LogAsync(ctx, execution, AgentLogLevel.Info, "Iniciando limpieza mensual de base de datos");

        int articulosEliminados = await ctx.NewsArticles
            .Where(a => a.PublishedAt < corteArticulos)
            .ExecuteDeleteAsync(ct);

        await LogAsync(ctx, execution, AgentLogLevel.Info,
            $"Artículos eliminados (>30 días): {articulosEliminados}");

        int auditLogsEliminados = await ctx.AuditLogs
            .Where(l => l.CreatedAt < corteAuditLogs)
            .ExecuteDeleteAsync(ct);

        await LogAsync(ctx, execution, AgentLogLevel.Info,
            $"Audit logs eliminados (>90 días): {auditLogsEliminados}");

        int agentLogsEliminados = await ctx.AgentLogs
            .Where(l => l.Timestamp < corteAgentLogs)
            .ExecuteDeleteAsync(ct);

        await LogAsync(ctx, execution, AgentLogLevel.Info,
            $"Agent logs eliminados (>30 días): {agentLogsEliminados}");

        execution.ItemsProcessed = articulosEliminados + auditLogsEliminados + agentLogsEliminados;

        string resumen = $"Limpieza completada: {articulosEliminados} artículos, " +
                         $"{auditLogsEliminados} audit logs, {agentLogsEliminados} agent logs eliminados";

        await LogAsync(ctx, execution, AgentLogLevel.Info, resumen);
        _logger.LogInformation("[DatabaseCleanupAgent] {Resumen}", resumen);
    }
}
