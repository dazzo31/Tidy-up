using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using TidyUp.Data.Entities;
using TidyUp.Data.Repositories;

namespace TidyUp.Services.Diagnostics;

/// <summary>
/// Default implementation of <see cref="IOperationTelemetryService"/> that publishes committed
/// execution events, deduplicates duplicate deliveries, and persists them into the action log database.
/// </summary>
public class OperationTelemetryService : IOperationTelemetryService
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IActionLogRepository? _directLogRepository;
    private readonly ConcurrentDictionary<Guid, byte> _processedActionIds = new();

    public event EventHandler<OperationTelemetryEvent>? OperationExecuted;

    /// <summary>
    /// DI constructor using service scope factory for safe multi-threaded database persistence.
    /// </summary>
    [ActivatorUtilitiesConstructor]
    public OperationTelemetryService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Standalone test constructor taking direct repository instance.
    /// </summary>
    public OperationTelemetryService(IActionLogRepository logRepository)
    {
        _directLogRepository = logRepository;
    }

    public async Task RecordOperationAsync(OperationTelemetryEvent telemetryEvent)
    {
        ArgumentNullException.ThrowIfNull(telemetryEvent);

        // Deduplicate notifications by ActionId
        if (!_processedActionIds.TryAdd(telemetryEvent.ActionId, 0))
        {
            return;
        }

        // Limit cache size to avoid memory growth over millions of operations
        if (_processedActionIds.Count > 10000)
        {
            _processedActionIds.Clear();
            _processedActionIds.TryAdd(telemetryEvent.ActionId, 0);
        }

        // 1. Persist to ActionLogEntity
        try
        {
            var logEntity = new ActionLogEntity
            {
                Id = telemetryEvent.ActionId,
                Timestamp = telemetryEvent.Timestamp,
                RuleId = telemetryEvent.RuleId ?? Guid.Empty,
                RuleName = string.IsNullOrWhiteSpace(telemetryEvent.RuleName) ? "Manual / System" : telemetryEvent.RuleName,
                FilePath = telemetryEvent.SourcePath,
                ActionPerformed = telemetryEvent.ActionType,
                ResultPath = telemetryEvent.TargetPath,
                Status = telemetryEvent.Outcome,
                ErrorMessage = telemetryEvent.ErrorMessage
            };

            if (_directLogRepository != null)
            {
                await _directLogRepository.AddAsync(logEntity);
            }
            else if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetService<IActionLogRepository>();
                if (repo != null)
                {
                    await repo.AddAsync(logEntity);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OperationTelemetryService] Failed to persist action log: {ex.Message}");
        }

        // 2. Publish stream to live subscribers (Dashboard, Logs, etc.)
        try
        {
            OperationExecuted?.Invoke(this, telemetryEvent);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[OperationTelemetryService] Subscriber threw exception: {ex.Message}");
        }
    }
}
