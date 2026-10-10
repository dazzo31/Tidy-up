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
    private volatile bool _isExecuting;

    public bool IsExecuting => _isExecuting;

    public BatchProcessingCoordinator(
        IActionExecutor actionExecutor,
        IApplicationStateManager? stateManager = null)
    {
        _actionExecutor = actionExecutor ?? throw new ArgumentNullException(nameof(actionExecutor));
        _stateManager = stateManager;
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

        _isExecuting = true;
        _stateManager?.TryTransitionTo(ApplicationLifecycleState.ExecutingBatch, "Batch execution started");

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
                        // Atomic operation: executed to completion. Thread.Abort is strictly prohibited.
                        // Even if cancellationToken is requested during this execution,
                        // this atomic file action completes cleanly and journals safely.
                        actionResult = await _actionExecutor.ExecuteActionAsync(
                            currentAction.FileAction,
                            fileInfo,
                            counter: i + 1,
                            batchId: batchId);
                    }
                }
                finally
                {
                    _stateManager?.NotifyOperationCompleted();
                }

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
            _isExecuting = false;
            _stateManager?.TryTransitionTo(ApplicationLifecycleState.Idle, "Batch execution ended");
        }

        return result;
    }
}

