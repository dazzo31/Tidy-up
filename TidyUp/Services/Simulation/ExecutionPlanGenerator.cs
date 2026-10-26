using System.IO;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;

namespace TidyUp.Services.Simulation;

/// <summary>
/// Generates an in-memory execution plan predicting what will happen when rules execute.
/// Maintains an in-memory batch target reservation ledger to detect collisions and project policy resolutions.
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

        var targetReservations = new Dictionary<string, PlannedFileAction>(StringComparer.OrdinalIgnoreCase);

        foreach (var folder in rule.MonitoredFolders)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                combinedPlan.IsIncomplete = true;
                combinedPlan.ScanErrors.Add("Scan was cancelled before all monitored folders could be scanned.");
                break;
            }

            if (string.IsNullOrWhiteSpace(folder.Path) || !Directory.Exists(folder.Path))
                continue;

            var folderPlan = await GeneratePlanForFolderInternalAsync(
                rule,
                folder.Path,
                folder.IncludeSubfolders,
                folder.ExclusionPatterns,
                targetReservations,
                cancellationToken);

            combinedPlan.TotalScanned += folderPlan.TotalScanned;
            combinedPlan.TotalMatched += folderPlan.TotalMatched;
            combinedPlan.PlannedActions.AddRange(folderPlan.PlannedActions);

            if (folderPlan.IsIncomplete)
                combinedPlan.IsIncomplete = true;
            combinedPlan.ScanErrors.AddRange(folderPlan.ScanErrors);
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
        var targetReservations = new Dictionary<string, PlannedFileAction>(StringComparer.OrdinalIgnoreCase);
        return GeneratePlanForFolderInternalAsync(
            rule,
            folderPath,
            includeSubfolders,
            exclusionPatterns,
            targetReservations,
            cancellationToken);
    }

    private async Task<ExecutionPlan> GeneratePlanForFolderInternalAsync(
        Rule rule,
        string folderPath,
        bool includeSubfolders,
        IEnumerable<string>? exclusionPatterns,
        Dictionary<string, PlannedFileAction> targetReservations,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var plan = new ExecutionPlan
        {
            RuleId = rule.Id,
            RuleName = rule.Name
        };

        if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            return plan;

        var exclusions = (exclusionPatterns ?? Enumerable.Empty<string>()).ToList();

        // Offload directory enumeration to background task to keep UI responsive
        await Task.Run(() =>
        {
            if (cancellationToken.IsCancellationRequested)
            {
                plan.IsIncomplete = true;
                plan.ScanErrors.Add("Folder scan was cancelled before starting.");
                return;
            }

            var dirQueue = new Queue<string>();
            dirQueue.Enqueue(folderPath);
            int fileCounter = 1;

            while (dirQueue.Count > 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    plan.IsIncomplete = true;
                    plan.ScanErrors.Add("Folder scan was cancelled before completion.");
                    break;
                }

                var currentDir = dirQueue.Dequeue();

                // 1. Enumerate files in currentDir with per-directory fault tolerance
                IEnumerable<string> filePaths;
                try
                {
                    filePaths = Directory.EnumerateFiles(currentDir, "*.*");
                }
                catch (UnauthorizedAccessException ex)
                {
                    plan.IsIncomplete = true;
                    plan.ScanErrors.Add($"Access denied to folder '{currentDir}': {ex.Message}");
                    continue;
                }
                catch (Exception ex)
                {
                    plan.IsIncomplete = true;
                    plan.ScanErrors.Add($"Error accessing folder '{currentDir}': {ex.Message}");
                    continue;
                }

                foreach (var filePath in filePaths)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        plan.IsIncomplete = true;
                        plan.ScanErrors.Add("File enumeration was cancelled.");
                        break;
                    }

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

                    // Evaluate rule conditions in simulation mode (decoupled from live execution eligibility)
                    bool isMatch = _ruleEngine.MatchesConditions(rule, fileInfo);
                    if (!isMatch)
                        continue;

                    plan.TotalMatched++;

                    // Track the simulated path across multi-action chains
                    string currentVirtualPath = fileInfo.FullName;
                    string currentVirtualName = fileInfo.Name;
                    Guid? previousActionId = null;

                    foreach (var action in rule.Actions.OrderBy(a => a.Order))
                    {
                        action.RuleId ??= rule.Id;
                        action.RuleName ??= rule.Name;
                        var plannedAction = SimulateAction(
                            action,
                            rule.Name,
                            fileInfo,
                            currentVirtualPath,
                            currentVirtualName,
                            fileCounter,
                            targetReservations,
                            folderPath);

                        plannedAction.InitialSourcePath = fileInfo.FullName;
                        plannedAction.DependsOnActionId = previousActionId;
                        plannedAction.RuleId = rule.Id;
                        plannedAction.PlannedCounter = fileCounter;
                        if (!string.IsNullOrEmpty(plannedAction.TargetPath))
                        {
                            plannedAction.TargetFileName = Path.GetFileName(plannedAction.TargetPath);
                        }

                        plan.PlannedActions.Add(plannedAction);
                        previousActionId = plannedAction.Id;

                        // Update virtual path for subsequent chained actions based on action semantics
                        switch (action)
                        {
                            case MoveFileAction when !string.IsNullOrEmpty(plannedAction.TargetPath):
                                currentVirtualPath = plannedAction.TargetPath;
                                currentVirtualName = Path.GetFileName(currentVirtualPath);
                                break;

                            case CopyFileAction copyAction:
                                if (!copyAction.ApplyToSourceFile && !string.IsNullOrEmpty(plannedAction.TargetPath))
                                {
                                    currentVirtualPath = plannedAction.TargetPath;
                                    currentVirtualName = Path.GetFileName(currentVirtualPath);
                                }
                                break;

                            case RenameFileAction when !string.IsNullOrEmpty(plannedAction.TargetPath):
                                currentVirtualPath = plannedAction.TargetPath;
                                currentVirtualName = Path.GetFileName(currentVirtualPath);
                                break;

                            case ChangeExtensionAction when !string.IsNullOrEmpty(plannedAction.TargetPath):
                                currentVirtualPath = plannedAction.TargetPath;
                                currentVirtualName = Path.GetFileName(currentVirtualPath);
                                break;

                            case DeleteFileAction:
                                currentVirtualPath = string.Empty;
                                currentVirtualName = string.Empty;
                                break;

                            case ExtractArchiveAction:
                                break;

                            case RunCommandAction:
                                break;
                        }
                    }

                    fileCounter++;
                }

                // 2. If includeSubfolders, enqueue accessible child subdirectories
                if (includeSubfolders)
                {
                    try
                    {
                        foreach (var subDir in Directory.EnumerateDirectories(currentDir))
                        {
                            if (cancellationToken.IsCancellationRequested)
                                break;

                            dirQueue.Enqueue(subDir);
                        }
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        plan.IsIncomplete = true;
                        plan.ScanErrors.Add($"Access denied to subdirectories in '{currentDir}': {ex.Message}");
                    }
                    catch (Exception ex)
                    {
                        plan.IsIncomplete = true;
                        plan.ScanErrors.Add($"Error scanning subdirectories in '{currentDir}': {ex.Message}");
                    }
                }
            }
        });

        return plan;
    }

    private PlannedFileAction SimulateAction(
        FileAction action,
        string ruleName,
        FileInfo fileInfo,
        string currentVirtualPath,
        string currentVirtualName,
        int counter,
        Dictionary<string, PlannedFileAction> targetReservations,
        string? monitoredRoot = null)
    {
        var plannedAction = new PlannedFileAction
        {
            SourcePath = currentVirtualPath,
            FileAction = action,
            RuleName = ruleName,
            FileSizeBytes = fileInfo.Length,
            SourceModifiedDate = fileInfo.LastWriteTime,
            Order = action.Order,
            MonitoredSourceRoot = monitoredRoot,
            PlannedCounter = counter
        };

        switch (action)
        {
            case MoveFileAction moveAction:
                moveAction.MonitoredSourceRoot ??= monitoredRoot;
                plannedAction.ActionType = ActionType.Move;
                plannedAction.ConflictResolution = moveAction.ConflictResolution;
                var moveDestDir = _variableEngine.Resolve(moveAction.DestinationPath, fileInfo, counter);
                if (moveAction.PreserveSubfolderStructure && !string.IsNullOrWhiteSpace(monitoredRoot) && fileInfo.DirectoryName != null)
                {
                    var relSub = GetSafeRelativeSubfolder(monitoredRoot, fileInfo.DirectoryName);
                    if (!string.IsNullOrEmpty(relSub))
                    {
                        moveDestDir = Path.Combine(moveDestDir, relSub);
                    }
                }
                plannedAction.TargetPath = Path.Combine(moveDestDir, currentVirtualName);
                CheckAndResolveConflict(plannedAction, currentVirtualPath, targetReservations);
                break;

            case CopyFileAction copyAction:
                copyAction.MonitoredSourceRoot ??= monitoredRoot;
                plannedAction.ActionType = ActionType.Copy;
                plannedAction.ConflictResolution = copyAction.ConflictResolution;
                var copyDestDir = _variableEngine.Resolve(copyAction.DestinationPath, fileInfo, counter);
                if (copyAction.PreserveSubfolderStructure && !string.IsNullOrWhiteSpace(monitoredRoot) && fileInfo.DirectoryName != null)
                {
                    var relSub = GetSafeRelativeSubfolder(monitoredRoot, fileInfo.DirectoryName);
                    if (!string.IsNullOrEmpty(relSub))
                    {
                        copyDestDir = Path.Combine(copyDestDir, relSub);
                    }
                }
                plannedAction.TargetPath = Path.Combine(copyDestDir, currentVirtualName);
                CheckAndResolveConflict(plannedAction, currentVirtualPath, targetReservations);
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
                CheckAndResolveConflict(plannedAction, currentVirtualPath, targetReservations);
                break;

            case ChangeExtensionAction changeExtAction:
                plannedAction.ActionType = ActionType.ChangeExtension;
                plannedAction.ConflictResolution = changeExtAction.ConflictResolution;
                var newExt = changeExtAction.NewExtension.TrimStart('.');
                var nameWithoutExt = Path.GetFileNameWithoutExtension(currentVirtualName);
                var dir = Path.GetDirectoryName(currentVirtualPath) ?? string.Empty;
                plannedAction.TargetPath = Path.Combine(dir, $"{nameWithoutExt}.{newExt}");
                CheckAndResolveConflict(plannedAction, currentVirtualPath, targetReservations);
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
                if (!string.IsNullOrEmpty(plannedAction.TargetPath) && Directory.Exists(plannedAction.TargetPath))
                {
                    if (extractAction.OverwriteExisting && Directory.EnumerateFileSystemEntries(plannedAction.TargetPath).Any())
                    {
                        plannedAction.HasConflict = true;
                        plannedAction.ConflictDescription = "Target directory already exists and contains files.";
                        plannedAction.ConflictResolution = ConflictResolution.Overwrite;
                        plannedAction.ResolutionNote = "Extraction destination directory contains existing files that may be overwritten.";
                    }
                    else
                    {
                        plannedAction.HasConflict = false;
                    }
                }
                else
                {
                    plannedAction.HasConflict = false;
                }
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

    private static void CheckAndResolveConflict(
        PlannedFileAction plannedAction,
        string sourcePath,
        Dictionary<string, PlannedFileAction> targetReservations)
    {
        if (string.IsNullOrWhiteSpace(plannedAction.TargetPath))
            return;

        string normalizedTarget;
        try
        {
            normalizedTarget = Path.GetFullPath(plannedAction.TargetPath);
        }
        catch
        {
            normalizedTarget = plannedAction.TargetPath;
        }

        // If destination path is identical to source, not a destination conflict
        if (string.Equals(normalizedTarget, sourcePath, StringComparison.OrdinalIgnoreCase))
            return;

        bool existsOnDisk = File.Exists(normalizedTarget);
        bool hasBatchReservation = targetReservations.TryGetValue(normalizedTarget, out var existingReservation);

        // Check if existing reservation is from the same chain (e.g. this action depends on it or has same initial source)
        bool isChainedPredecessor = hasBatchReservation &&
            (plannedAction.DependsOnActionId == existingReservation!.Id ||
             string.Equals(plannedAction.InitialSourcePath, existingReservation.InitialSourcePath, StringComparison.OrdinalIgnoreCase));

        bool isConflict = existsOnDisk || (hasBatchReservation && !isChainedPredecessor);

        if (!isConflict)
        {
            plannedAction.HasConflict = false;
            targetReservations[normalizedTarget] = plannedAction;
            return;
        }

        // Handle conflict according to policy
        switch (plannedAction.ConflictResolution)
        {
            case ConflictResolution.RenameNew:
                // Auto-rename by projecting next available (1), (2), ...
                var dir = Path.GetDirectoryName(normalizedTarget) ?? string.Empty;
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(normalizedTarget);
                var ext = Path.GetExtension(normalizedTarget);
                string candidate = normalizedTarget;
                int counter = 1;

                while (File.Exists(candidate) || targetReservations.ContainsKey(candidate))
                {
                    candidate = Path.Combine(dir, $"{fileNameWithoutExt} ({counter}){ext}");
                    counter++;
                }

                plannedAction.TargetPath = candidate;
                plannedAction.HasConflict = false;
                plannedAction.ResolutionNote = $"Auto-renamed due to conflict from '{Path.GetFileName(normalizedTarget)}'";
                targetReservations[candidate] = plannedAction;
                break;

            case ConflictResolution.Skip:
                plannedAction.IsSkipped = true;
                plannedAction.HasConflict = false;
                plannedAction.ConflictDescription = existsOnDisk
                    ? $"Skipped by policy: destination file already exists ({Path.GetFileName(normalizedTarget)})"
                    : $"Skipped by policy: destination reserved by another action in batch ({Path.GetFileName(normalizedTarget)})";
                break;

            case ConflictResolution.Overwrite:
                if (hasBatchReservation && !isChainedPredecessor)
                {
                    // Two independent actions in the same batch claiming the identical target is an unresolvable collision!
                    plannedAction.HasConflict = true;
                    plannedAction.ConflictDescription = $"Batch conflict: multiple actions in batch target identical destination '{Path.GetFileName(normalizedTarget)}'";
                }
                else
                {
                    plannedAction.HasConflict = false;
                    plannedAction.ResolutionNote = "Overwrites existing file on disk (requires execution authorization)";
                    targetReservations[normalizedTarget] = plannedAction;
                }
                break;

            case ConflictResolution.RenameOld:
                if (hasBatchReservation && !isChainedPredecessor)
                {
                    plannedAction.HasConflict = true;
                    plannedAction.ConflictDescription = $"Batch conflict: multiple actions in batch target identical destination '{Path.GetFileName(normalizedTarget)}'";
                }
                else
                {
                    plannedAction.HasConflict = false;
                    plannedAction.ResolutionNote = "Existing destination will be backed up with timestamp suffix before replacement";
                    targetReservations[normalizedTarget] = plannedAction;
                }
                break;

            case ConflictResolution.Prompt:
            default:
                plannedAction.HasConflict = true;
                plannedAction.ConflictDescription = existsOnDisk
                    ? $"Target file already exists: {normalizedTarget}"
                    : $"Batch conflict: target already reserved by another action: {normalizedTarget}";
                break;
        }
    }

    /// <summary>
    /// Computes the relative subfolder of a file within a monitored root while verifying directory confinement.
    /// Returns null if the file directory is at the root, escapes the root, or is invalid.
    /// </summary>
    public static string? GetSafeRelativeSubfolder(string monitoredRoot, string fileDirectory)
    {
        try
        {
            var fullRoot = Path.GetFullPath(monitoredRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var fullDir = Path.GetFullPath(fileDirectory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            // Case-insensitive prefix confinement check
            if (!fullDir.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var relative = Path.GetRelativePath(fullRoot, fullDir);
            if (string.IsNullOrWhiteSpace(relative) ||
                relative == "." ||
                relative.StartsWith("..", StringComparison.Ordinal) ||
                Path.IsPathRooted(relative))
            {
                return null;
            }

            return relative;
        }
        catch
        {
            return null;
        }
    }
}
