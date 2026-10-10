using System.IO;
using TidyUp.Data.Entities;
using TidyUp.Models.Domain;

namespace TidyUp.Services.Diagnostics;

/// <summary>
/// Compares planned simulation actions against physical execution results,
/// categorizing discrepancies such as locks, permission denials, missing files, or modified content.
/// </summary>
public class ExecutionDiscrepancyAnalyzer : IExecutionDiscrepancyAnalyzer
{
    public delegate (bool Exists, DateTime LastWriteTime, long Size) FileStateProvider(string path);

    private readonly FileStateProvider _fileStateProvider;

    public ExecutionDiscrepancyAnalyzer(FileStateProvider? fileStateProvider = null)
    {
        _fileStateProvider = fileStateProvider ?? DefaultFileStateProvider;
    }

    private static (bool Exists, DateTime LastWriteTime, long Size) DefaultFileStateProvider(string path)
    {
        try
        {
            var info = new FileInfo(path);
            return (info.Exists, info.Exists ? info.LastWriteTimeUtc : DateTime.MinValue, info.Exists ? info.Length : 0);
        }
        catch
        {
            return (false, DateTime.MinValue, 0);
        }
    }

    public ExecutionDiscrepancyReport Analyze(
        ExecutionPlan plan,
        IEnumerable<OperationJournalEntry> journalEntries,
        Guid? batchId = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return Analyze(plan.PlannedActions, journalEntries, batchId ?? plan.PlanId);
    }

    public ExecutionDiscrepancyReport Analyze(
        IEnumerable<PlannedFileAction> plannedActions,
        IEnumerable<OperationJournalEntry> journalEntries,
        Guid? batchId = null)
    {
        var plannedList = plannedActions?.ToList() ?? new List<PlannedFileAction>();
        var journalDict = (journalEntries ?? Enumerable.Empty<OperationJournalEntry>())
            .GroupBy(j => j.OriginalPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last(), StringComparer.OrdinalIgnoreCase);

        var items = new List<ActionDiscrepancyItem>();

        foreach (var action in plannedList)
        {
            journalDict.TryGetValue(action.SourcePath, out var journal);
            items.Add(EvaluateActionAgainstJournal(action, journal));
        }

        return BuildReport(items, batchId ?? Guid.NewGuid());
    }

    public ExecutionDiscrepancyReport AnalyzeFromResults(
        IEnumerable<PlannedFileAction> plannedActions,
        IEnumerable<(PlannedFileAction Action, ActionResult Result)> executionResults,
        Guid? batchId = null)
    {
        var plannedList = plannedActions?.ToList() ?? new List<PlannedFileAction>();
        var resultsDict = (executionResults ?? Enumerable.Empty<(PlannedFileAction Action, ActionResult Result)>())
            .GroupBy(r => r.Action.SourcePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Result, StringComparer.OrdinalIgnoreCase);

        var items = new List<ActionDiscrepancyItem>();

        foreach (var action in plannedList)
        {
            resultsDict.TryGetValue(action.SourcePath, out var result);
            items.Add(EvaluateActionAgainstResult(action, result));
        }

        return BuildReport(items, batchId ?? Guid.NewGuid());
    }

    private ActionDiscrepancyItem EvaluateActionAgainstJournal(PlannedFileAction action, OperationJournalEntry? journal)
    {
        if (journal == null)
        {
            var state = _fileStateProvider(action.SourcePath);
            if (!state.Exists)
            {
                return new ActionDiscrepancyItem
                {
                    PlannedActionId = action.Id,
                    SourcePath = action.SourcePath,
                    PlannedTargetPath = action.TargetPath,
                    ActionType = action.ActionType,
                    Outcome = DiscrepancyOutcome.FileDisappeared,
                    Explanation = $"Source file was deleted or moved before execution: '{action.SourcePath}'",
                    PreviewModifiedTime = action.SourceModifiedDate,
                    PreviewFileSize = action.FileSizeBytes
                };
            }

            return new ActionDiscrepancyItem
            {
                PlannedActionId = action.Id,
                SourcePath = action.SourcePath,
                PlannedTargetPath = action.TargetPath,
                ActionType = action.ActionType,
                Outcome = DiscrepancyOutcome.NotExecuted,
                Explanation = "Planned action was not executed in this batch (unapproved or cancelled).",
                PreviewModifiedTime = action.SourceModifiedDate,
                PreviewFileSize = action.FileSizeBytes
            };
        }

        var isSuccess = string.Equals(journal.Status, "Completed", StringComparison.OrdinalIgnoreCase);
        var details = journal.Details ?? string.Empty;

        if (!isSuccess)
        {
            var outcome = ClassifyErrorDetails(details);
            return new ActionDiscrepancyItem
            {
                PlannedActionId = action.Id,
                SourcePath = action.SourcePath,
                PlannedTargetPath = action.TargetPath,
                ActualTargetPath = journal.TargetPath,
                ActionType = action.ActionType,
                Outcome = outcome,
                Explanation = string.IsNullOrWhiteSpace(details)
                    ? $"Operation failed with status: {journal.Status}"
                    : details,
                PreviewModifiedTime = action.SourceModifiedDate,
                PreviewFileSize = action.FileSizeBytes
            };
        }

        // Operation marked Completed - check for modifications between preview and execute
        if (details.Contains("ModifiedSincePreview", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("modified after preview", StringComparison.OrdinalIgnoreCase))
        {
            return new ActionDiscrepancyItem
            {
                PlannedActionId = action.Id,
                SourcePath = action.SourcePath,
                PlannedTargetPath = action.TargetPath,
                ActualTargetPath = journal.TargetPath,
                ActionType = action.ActionType,
                Outcome = DiscrepancyOutcome.ModifiedSincePreview,
                Explanation = string.IsNullOrWhiteSpace(details)
                    ? "File was modified on disk after preview was generated."
                    : details,
                PreviewModifiedTime = action.SourceModifiedDate,
                PreviewFileSize = action.FileSizeBytes
            };
        }

        return new ActionDiscrepancyItem
        {
            PlannedActionId = action.Id,
            SourcePath = action.SourcePath,
            PlannedTargetPath = action.TargetPath,
            ActualTargetPath = journal.TargetPath ?? action.TargetPath,
            ActionType = action.ActionType,
            Outcome = DiscrepancyOutcome.ExecutedAsPlanned,
            Explanation = $"Action '{action.ActionType}' executed exactly as planned.",
            PreviewModifiedTime = action.SourceModifiedDate,
            PreviewFileSize = action.FileSizeBytes
        };
    }

    private ActionDiscrepancyItem EvaluateActionAgainstResult(PlannedFileAction action, ActionResult? result)
    {
        if (result == null)
        {
            var state = _fileStateProvider(action.SourcePath);
            if (!state.Exists)
            {
                return new ActionDiscrepancyItem
                {
                    PlannedActionId = action.Id,
                    SourcePath = action.SourcePath,
                    PlannedTargetPath = action.TargetPath,
                    ActionType = action.ActionType,
                    Outcome = DiscrepancyOutcome.FileDisappeared,
                    Explanation = $"Source file was deleted or moved before execution: '{action.SourcePath}'",
                    PreviewModifiedTime = action.SourceModifiedDate,
                    PreviewFileSize = action.FileSizeBytes
                };
            }

            return new ActionDiscrepancyItem
            {
                PlannedActionId = action.Id,
                SourcePath = action.SourcePath,
                PlannedTargetPath = action.TargetPath,
                ActionType = action.ActionType,
                Outcome = DiscrepancyOutcome.NotExecuted,
                Explanation = "Planned action was not executed in this batch.",
                PreviewModifiedTime = action.SourceModifiedDate,
                PreviewFileSize = action.FileSizeBytes
            };
        }

        if (!result.Success)
        {
            var error = result.ErrorMessage ?? string.Empty;
            var outcome = ClassifyErrorDetails(error);

            return new ActionDiscrepancyItem
            {
                PlannedActionId = action.Id,
                SourcePath = action.SourcePath,
                PlannedTargetPath = action.TargetPath,
                ActualTargetPath = result.ResultPath ?? result.TargetPath,
                ActionType = action.ActionType,
                Outcome = outcome,
                Explanation = string.IsNullOrWhiteSpace(error)
                    ? "Action execution failed."
                    : error,
                PreviewModifiedTime = action.SourceModifiedDate,
                PreviewFileSize = action.FileSizeBytes
            };
        }

        // Check if file was modified between preview and execute
        var dest = result.ResultPath ?? result.TargetPath ?? action.TargetPath ?? action.SourcePath;
        var currentState = _fileStateProvider(dest);
        if (action.SourceModifiedDate != default && currentState.Exists &&
            Math.Abs((currentState.LastWriteTime - action.SourceModifiedDate).TotalSeconds) > 2)
        {
            return new ActionDiscrepancyItem
            {
                PlannedActionId = action.Id,
                SourcePath = action.SourcePath,
                PlannedTargetPath = action.TargetPath,
                ActualTargetPath = result.ResultPath ?? result.TargetPath ?? action.TargetPath,
                ActionType = action.ActionType,
                Outcome = DiscrepancyOutcome.ModifiedSincePreview,
                Explanation = $"File timestamp changed between preview ({action.SourceModifiedDate:g}) and execution ({currentState.LastWriteTime:g}).",
                PreviewModifiedTime = action.SourceModifiedDate,
                ExecutionModifiedTime = currentState.LastWriteTime,
                PreviewFileSize = action.FileSizeBytes,
                ExecutionFileSize = currentState.Size
            };
        }

        return new ActionDiscrepancyItem
        {
            PlannedActionId = action.Id,
            SourcePath = action.SourcePath,
            PlannedTargetPath = action.TargetPath,
            ActualTargetPath = result.ResultPath ?? result.TargetPath ?? action.TargetPath,
            ActionType = action.ActionType,
            Outcome = DiscrepancyOutcome.ExecutedAsPlanned,
            Explanation = $"Action '{action.ActionType}' executed exactly as planned.",
            PreviewModifiedTime = action.SourceModifiedDate,
            PreviewFileSize = action.FileSizeBytes
        };
    }

    private static DiscrepancyOutcome ClassifyErrorDetails(string details)
    {
        if (string.IsNullOrWhiteSpace(details))
            return DiscrepancyOutcome.FailedGeneralError;

        if (details.Contains("lock", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("in use", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("being used", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("sharing violation", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("held open", StringComparison.OrdinalIgnoreCase))
        {
            return DiscrepancyOutcome.SkippedDueToLock;
        }

        if (details.Contains("access", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("permission", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            return DiscrepancyOutcome.FailedDueToPermission;
        }

        if (details.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("could not find", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
            details.Contains("disappeared", StringComparison.OrdinalIgnoreCase))
        {
            return DiscrepancyOutcome.FileDisappeared;
        }

        return DiscrepancyOutcome.FailedGeneralError;
    }

    private static ExecutionDiscrepancyReport BuildReport(List<ActionDiscrepancyItem> items, Guid batchId)
    {
        var total = items.Count;
        var executedAsPlanned = items.Count(i => i.Outcome == DiscrepancyOutcome.ExecutedAsPlanned);
        var discrepancies = total - executedAsPlanned;

        var summary = discrepancies == 0
            ? $"All {total} planned action(s) executed exactly as previewed with 0 discrepancies."
            : $"{executedAsPlanned} of {total} action(s) executed as planned ({discrepancies} discrepanc{(discrepancies == 1 ? "y" : "ies")} detected).";

        return new ExecutionDiscrepancyReport
        {
            BatchId = batchId,
            AnalyzedAtUtc = DateTime.UtcNow,
            TotalPlanned = total,
            ExecutedAsPlannedCount = executedAsPlanned,
            DiscrepancyCount = discrepancies,
            Items = items,
            SummaryText = summary
        };
    }
}
