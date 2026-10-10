using System.IO;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;

namespace TidyUp.Services.Simulation;

/// <summary>
/// Implements read-only dry-run simulation of rules against target file paths.
/// Produces an in-memory ExecutionPlan without modifying or creating any files or database state.
/// </summary>
public class ExecutionPlanGenerator : IExecutionPlanGenerator
{
    private readonly IRuleEngine _ruleEngine;
    private readonly IVariableEngine _variableEngine;

    public ExecutionPlanGenerator(IRuleEngine ruleEngine, IVariableEngine variableEngine)
    {
        _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
        _variableEngine = variableEngine ?? throw new ArgumentNullException(nameof(variableEngine));
    }

    public async Task<ExecutionPlan> GeneratePlanAsync(Rule rule, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var combinedPlan = new ExecutionPlan
        {
            RuleId = rule.Id,
            RuleName = rule.Name
        };

        foreach (var folder in rule.MonitoredFolders)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (string.IsNullOrWhiteSpace(folder.Path) || !Directory.Exists(folder.Path))
                continue;

            var folderPlan = await GeneratePlanForFolderAsync(
                rule,
                folder.Path,
                folder.IncludeSubfolders,
                folder.ExclusionPatterns,
                cancellationToken);

            combinedPlan.TotalScanned += folderPlan.TotalScanned;
            combinedPlan.TotalMatched += folderPlan.TotalMatched;
            combinedPlan.PlannedActions.AddRange(folderPlan.PlannedActions);
        }

        return combinedPlan;
    }

    public Task<ExecutionPlan> GeneratePlanForFolderAsync(
        Rule rule,
        string folderPath,
        bool includeSubfolders,
        IEnumerable<string>? exclusionPatterns = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var plan = new ExecutionPlan
        {
            RuleId = rule.Id,
            RuleName = rule.Name
        };

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return Task.FromResult(plan);

        var exclusions = (exclusionPatterns ?? Enumerable.Empty<string>()).ToList();
        var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

        string[] filePaths;
        try
        {
            filePaths = Directory.GetFiles(folderPath, "*.*", searchOption);
        }
        catch (Exception)
        {
            // If directory cannot be accessed, return empty plan
            return Task.FromResult(plan);
        }

        int fileCounter = 1;

        foreach (var filePath in filePaths)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            plan.TotalScanned++;

            // Check exclusion patterns
            if (exclusions.Any(p => !string.IsNullOrEmpty(p) && filePath.Contains(p, StringComparison.OrdinalIgnoreCase)))
                continue;

            FileInfo fileInfo;
            try
            {
                fileInfo = new FileInfo(filePath);
                if (!fileInfo.Exists)
                    continue;
            }
            catch (Exception)
            {
                continue;
            }

            // Evaluate rule conditions
            bool isMatch = _ruleEngine.EvaluateRule(rule, fileInfo);
            if (!isMatch)
                continue;

            plan.TotalMatched++;

            // Track the simulated path across multi-action chains
            string currentVirtualPath = fileInfo.FullName;
            string currentVirtualName = fileInfo.Name;

            foreach (var action in rule.Actions.OrderBy(a => a.Order))
            {
                var plannedAction = SimulateAction(
                    action,
                    rule.Name,
                    fileInfo,
                    currentVirtualPath,
                    currentVirtualName,
                    fileCounter);

                plan.PlannedActions.Add(plannedAction);

                // Update virtual path for subsequent chained actions if applicable
                if (!string.IsNullOrEmpty(plannedAction.TargetPath))
                {
                    currentVirtualPath = plannedAction.TargetPath;
                    currentVirtualName = Path.GetFileName(currentVirtualPath);
                }
            }

            fileCounter++;
        }

        return Task.FromResult(plan);
    }

    private PlannedFileAction SimulateAction(
        FileAction action,
        string ruleName,
        FileInfo fileInfo,
        string currentVirtualPath,
        string currentVirtualName,
        int counter)
    {
        var plannedAction = new PlannedFileAction
        {
            SourcePath = currentVirtualPath,
            FileAction = action,
            RuleName = ruleName,
            FileSizeBytes = fileInfo.Length,
            SourceModifiedDate = fileInfo.LastWriteTime,
            Order = action.Order
        };

        switch (action)
        {
            case MoveFileAction moveAction:
                plannedAction.ActionType = ActionType.Move;
                plannedAction.ConflictResolution = moveAction.ConflictResolution;
                var moveDestDir = _variableEngine.Resolve(moveAction.DestinationPath, fileInfo, counter);
                plannedAction.TargetPath = Path.Combine(moveDestDir, currentVirtualName);
                CheckConflict(plannedAction, currentVirtualPath);
                break;

            case CopyFileAction copyAction:
                plannedAction.ActionType = ActionType.Copy;
                plannedAction.ConflictResolution = copyAction.ConflictResolution;
                var copyDestDir = _variableEngine.Resolve(copyAction.DestinationPath, fileInfo, counter);
                plannedAction.TargetPath = Path.Combine(copyDestDir, currentVirtualName);
                CheckConflict(plannedAction, currentVirtualPath);
                break;

            case RenameFileAction renameAction:
                plannedAction.ActionType = ActionType.Rename;
                plannedAction.ConflictResolution = renameAction.ConflictResolution;
                var newName = _variableEngine.Resolve(renameAction.NamePattern, fileInfo, counter);
                if (!Path.HasExtension(newName) && Path.HasExtension(currentVirtualName))
                {
                    newName += Path.GetExtension(currentVirtualName);
                }
                var parentDir = Path.GetDirectoryName(currentVirtualPath) ?? string.Empty;
                plannedAction.TargetPath = Path.Combine(parentDir, newName);
                CheckConflict(plannedAction, currentVirtualPath);
                break;

            case ChangeExtensionAction changeExtAction:
                plannedAction.ActionType = ActionType.ChangeExtension;
                plannedAction.ConflictResolution = changeExtAction.ConflictResolution;
                var newExt = changeExtAction.NewExtension.TrimStart('.');
                var nameWithoutExt = Path.GetFileNameWithoutExtension(currentVirtualName);
                var dir = Path.GetDirectoryName(currentVirtualPath) ?? string.Empty;
                plannedAction.TargetPath = Path.Combine(dir, $"{nameWithoutExt}.{newExt}");
                CheckConflict(plannedAction, currentVirtualPath);
                break;

            case DeleteFileAction deleteAction:
                plannedAction.ActionType = ActionType.Delete;
                plannedAction.TargetPath = null;
                plannedAction.HasConflict = false;
                break;

            case ExtractArchiveAction extractAction:
                plannedAction.ActionType = ActionType.ExtractArchive;
                var extractDest = _variableEngine.Resolve(extractAction.DestinationPath, fileInfo, counter);
                plannedAction.TargetPath = string.IsNullOrWhiteSpace(extractDest)
                    ? Path.GetDirectoryName(currentVirtualPath)
                    : extractDest;
                plannedAction.HasConflict = false;
                break;

            case RunCommandAction runCmdAction:
                plannedAction.ActionType = ActionType.RunCommand;
                plannedAction.TargetPath = _variableEngine.Resolve(runCmdAction.Command, fileInfo, counter);
                plannedAction.HasConflict = false;
                break;

            default:
                plannedAction.ActionType = ActionType.Move;
                plannedAction.TargetPath = null;
                break;
        }

        return plannedAction;
    }

    private static void CheckConflict(PlannedFileAction plannedAction, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(plannedAction.TargetPath))
            return;

        // If destination path is identical to source, not a destination conflict
        if (string.Equals(plannedAction.TargetPath, sourcePath, StringComparison.OrdinalIgnoreCase))
            return;

        if (File.Exists(plannedAction.TargetPath))
        {
            plannedAction.HasConflict = true;
            plannedAction.ConflictDescription = $"Target file already exists: {plannedAction.TargetPath}";
        }
    }
}

