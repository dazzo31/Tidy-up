using TidyUp.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.IO;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services.State;

namespace TidyUp.Services.Processing;

/// <summary>
/// Implementation of batch processing coordinator ensuring cooperative cancellation,
/// atomic in-flight operation completion, and state transition synchronization.
/// </summary>
public class BatchProcessingCoordinator : IBatchProcessingCoordinator
{
    private readonly IActionExecutor _actionExecutor;
    private readonly IApplicationStateManager? _stateManager;
    private readonly IRuleRepository? _ruleRepository;
    private readonly IServiceScopeFactory? _scopeFactory;
    private int _executingFlag;

    public bool IsExecuting => Volatile.Read(ref _executingFlag) == 1;

    public BatchProcessingCoordinator(
        IActionExecutor actionExecutor,
        IApplicationStateManager? stateManager = null,
        IRuleRepository? ruleRepository = null,
        IServiceScopeFactory? scopeFactory = null)
    {
        _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
        _stateManager = stateManager;
        _ruleRepository = ruleRepository;
        _scopeFactory = scopeFactory;
    }

    public async Task<BatchExecutionResult> ExecuteBatchAsync(
        IReadOnlyList<PlannedFileAction> actions,
        Guid batchId,
        IProgress<BatchProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var result = new BatchExecutionResult
        {
            BatchId = batchId,
            TotalItems = actions.Count,
            Status = BatchExecutionStatus.Running
        };

        if (actions.Count == 0)
        {
            result.Status = BatchExecutionStatus.Completed;
            return result;
        }

        if (Interlocked.CompareExchange(ref _executingFlag, 1, 0) != 0)
        {
            throw new InvalidOperationException("Another batch operation is already executing.");
        }

        bool acquiredLease = false;
        if (_stateManager is not null)
        {
            acquiredLease = _stateManager.TryAcquireBatchLease(batchId, "Batch execution started");
            if (!acquiredLease)
            {
                if (!_stateManager.TryTransitionTo(ApplicationLifecycleState.ExecutingBatch, "Batch execution started"))
                {
                    Interlocked.Exchange(ref _executingFlag, 0);
                    throw new InvalidOperationException("Cannot transition application state to ExecutingBatch.");
                }
            }
        }

        try
        {
            for (int i = 0; i < actions.Count; i++)
            {
                // Cooperative cancellation check: evaluate cancellation BEFORE scheduling the next item.
                // If cancellation is requested, cease scheduling subsequent operations,
                // mark remaining items as skipped/cancelled, and set status to PartiallyCompleted.
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Status = BatchExecutionStatus.PartiallyCompleted;
                    result.CancelledOrSkippedCount += (actions.Count - i);

                    for (int j = i; j < actions.Count; j++)
                    {
                        result.ItemResults.Add(new BatchItemExecutionResult
                        {
                            Action = actions[j],
                            Skipped = true,
                            Result = new ActionResult
                            {
                                Success = false,
                                ErrorMessage = "Cancelled prior to execution",
                                Type = ActionResultType.Warning
                            }
                        });
                    }

                    break;
                }

                var currentAction = actions[i];
                var fileInfo = new FileInfo(currentAction.SourcePath);
                ActionResult actionResult;

                _stateManager?.NotifyOperationStarted();

                try
                {
                    // Atomic operation: executed using immutable planned parameters (preserving exact previewed target paths & counter sequence).
                    // Even if cancellationToken is requested during this execution, this atomic file action completes cleanly and journals safely.
                    actionResult = await _actionExecutor.ExecutePlannedActionAsync(currentAction, batchId);
                    if (actionResult is null)
                    {
                        if (!fileInfo.Exists)
                        {
                            actionResult = new ActionResult
                            {
                                Success = false,
                                ErrorMessage = "Source file not found",
                                Type = ActionResultType.Error
                            };
                        }
                        else
                        {
                            // Fallback for mocks configuring only ExecuteActionAsync
                            actionResult = await _actionExecutor.ExecuteActionAsync(
                                currentAction.FileAction,
                                fileInfo,
                                counter: currentAction.PlannedCounter ?? (i + 1),
                                batchId: batchId);
                        }
                    }
                }
                finally
                {
                    _stateManager?.NotifyOperationCompleted();
                }

                actionResult ??= new ActionResult { Success = false, ErrorMessage = "No result returned", Type = ActionResultType.Error };
                if (actionResult.Success)
                {
                    result.SucceededCount++;
                }
                else
                {
                    result.FailedCount++;
                }

                result.ItemResults.Add(new BatchItemExecutionResult
                {
                    Action = currentAction,
                    Result = actionResult,
                    Skipped = false
                });

                progress?.Report(new BatchProgressReport
                {
                    CompletedCount = i + 1,
                    TotalCount = actions.Count,
                    CurrentAction = currentAction,
                    LastActionResult = actionResult
                });
            }

            if (result.Status == BatchExecutionStatus.Running)
            {
                result.Status = result.FailedCount > 0 && result.SucceededCount == 0
                    ? BatchExecutionStatus.Failed
                    : BatchExecutionStatus.Completed;
            }
        }
        catch (Exception ex)
        {
            result.Status = BatchExecutionStatus.Failed;
            result.ErrorMessage = ex.Message;
            throw;
        }
        finally
        {
            try
            {
                if (acquiredLease && _stateManager is not null)
                {
                    _stateManager.ReleaseBatchLease(batchId, "Batch execution ended");
                }
                else if (_stateManager is not null && _stateManager.CurrentState == ApplicationLifecycleState.ExecutingBatch)
                {
                    _stateManager.TryTransitionTo(ApplicationLifecycleState.Idle, "Batch execution ended");
                }
            }
            finally
            {
                Interlocked.Exchange(ref _executingFlag, 0);
            }
        }

        // Update rule execution stats (LastRunDate and FilesProcessedCount)
        await UpdateRuleStatisticsAsync(result.ItemResults);

        return result;
    }

    private async Task UpdateRuleStatisticsAsync(List<BatchItemExecutionResult> itemResults)
    {
        try
        {
            var successful = itemResults
                .Where(r => r.Result is { Success: true } && r.Action.RuleId.HasValue && r.Action.RuleId.Value != Guid.Empty)
                .ToList();

            if (successful.Count == 0) return;

            var groups = successful.GroupBy(r => r.Action.RuleId!.Value);
            var now = DateTime.UtcNow;

            if (_ruleRepository != null)
            {
                foreach (var g in groups)
                {
                    var distinctFiles = g.Select(r => r.Action.InitialSourcePath ?? r.Action.SourcePath)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Count();
                    await _ruleRepository.RecordRuleExecutionAsync(g.Key, distinctFiles, now);
                }
            }
            else if (_scopeFactory != null)
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetService<IRuleRepository>();
                if (repo != null)
                {
                    foreach (var g in groups)
                    {
                        var distinctFiles = g.Select(r => r.Action.InitialSourcePath ?? r.Action.SourcePath)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .Count();
                        await repo.RecordRuleExecutionAsync(g.Key, distinctFiles, now);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[BatchProcessingCoordinator] Failed to update rule execution stats: {ex.Message}");
        }
    }
}

