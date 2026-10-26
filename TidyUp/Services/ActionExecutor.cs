using System.Diagnostics;
using System.IO;
using Microsoft.VisualBasic.FileIO;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using TidyUp.Data.Entities;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services.FileSystem;
using TidyUp.Services.Rollback;

namespace TidyUp.Services;

/// <summary>
/// Implementation of file action executor with retry logic and conflict resolution.
/// </summary>
public class ActionExecutor(
    IVariableEngine variableEngine,
    ISafeFileSystem? safeFileSystem = null,
    IFileLockDetector? fileLockDetector = null,
    IRollbackEngine? rollbackEngine = null) : IActionExecutor
{
    private readonly ISafeFileSystem _safeFileSystem = safeFileSystem ?? new WindowsShellFileOperations();
    private readonly IFileLockDetector _fileLockDetector = fileLockDetector ?? new FileLockDetector();
    private readonly IRollbackEngine? _rollbackEngine = rollbackEngine;
    private const int MaxRetries = 3;
    private static readonly int[] RetryDelays = [100, 500, 2000];

    public Task<List<ActionResult>> ExecuteActionsAsync(
        List<FileAction> actions,
        FileInfo fileInfo,
        int? counter = null,
        Guid? batchId = null)
        => ExecuteActionsAsync(actions, fileInfo, counter, batchId, null, null, null);

    public async Task<List<ActionResult>> ExecuteActionsAsync(
        List<FileAction> actions,
        FileInfo fileInfo,
        int? counter,
        Guid? batchId,
        Guid? ruleId,
        string? ruleName,
        Guid? ruleRevisionId)
    {
        var effectiveBatchId = batchId ?? Guid.NewGuid();
        var results = new List<ActionResult>();
        var currentFile = fileInfo;

        foreach (var action in actions.OrderBy(a => a.Order))
        {                       
            var result = await ExecuteActionAsync(action, currentFile, counter, effectiveBatchId, ruleId, ruleName, ruleRevisionId);
            results.Add(result);

            if (!result.Success)
                break;

            if (!string.IsNullOrEmpty(result.ResultPath) && File.Exists(result.ResultPath))
                currentFile = new FileInfo(result.ResultPath);
        }

        return results;
    }

    public Task<ActionResult> ExecuteActionAsync(
        FileAction action,
        FileInfo fileInfo,
        int? counter = null,
        Guid? batchId = null)
        => ExecuteActionAsync(action, fileInfo, counter, batchId, null, null, null);

    public async Task<ActionResult> ExecuteActionAsync(
        FileAction action,
        FileInfo fileInfo,
        int? counter,
        Guid? batchId,
        Guid? ruleId,
        string? ruleName,
        Guid? ruleRevisionId)
    {
        var originalPath = fileInfo.FullName;
        var preActionHash = FileChecksumHelper.ComputeSha256(originalPath);
        ruleId ??= action.RuleId;
        ruleName ??= action.RuleName;

        var result = action switch
        {
            MoveFileAction move => await ExecuteMoveAsync(move, fileInfo, counter),
            CopyFileAction copy => await ExecuteCopyAsync(copy, fileInfo, counter),
            RenameFileAction rename => await ExecuteRenameAsync(rename, fileInfo, counter),
            ChangeExtensionAction changeExt => await ExecuteChangeExtensionAsync(changeExt, fileInfo),
            DeleteFileAction delete => await ExecuteDeleteAsync(delete, fileInfo, isAuthorizedForPermanentDeletion: !delete.ConfirmBeforeDelete),
            ExtractArchiveAction extract => await ExecuteExtractArchiveAsync(extract, fileInfo, counter, isAuthorizedForPermanentDeletion: !extract.ConfirmBeforeDelete),
            RunCommandAction runCmd => await ExecuteRunCommandAsync(runCmd, fileInfo, counter),
            _ => new ActionResult { Success = false, ErrorMessage = "Unknown action type", Type = ActionResultType.Error }
        };

        if (_rollbackEngine is not null)
        {
            var effectiveBatchId = batchId ?? Guid.NewGuid();
            var actionTypeName = action switch
            {
                MoveFileAction => "Move",
                CopyFileAction => "Copy",
                RenameFileAction => "Rename",
                ChangeExtensionAction => "ChangeExtension",
                DeleteFileAction => "Delete",
                ExtractArchiveAction => "ExtractArchive",
                RunCommandAction => "RunCommand",
                _ => action.GetType().Name
            };

            if (result.Success)
            {
                var postPath = result.TargetPath ?? (action is DeleteFileAction ? null : result.ResultPath ?? originalPath);
                var postActionHash = !string.IsNullOrEmpty(postPath) && File.Exists(postPath) ? FileChecksumHelper.ComputeSha256(postPath) : null;
                var details = result.DisplacedBackupPath is not null
                    ? $"Displaced file backed up to: {result.DisplacedBackupPath}"
                    : action is DeleteFileAction del
                        ? (del.UseRecycleBin ? "Sent to Recycle Bin" : "Permanently Deleted")
                        : null;

                await _rollbackEngine.RecordOperationAsync(new OperationJournalEntry
                {
                    BatchId = effectiveBatchId,
                    RuleId = ruleId,
                    RuleName = ruleName,
                    RuleRevisionId = ruleRevisionId,
                    ActionType = actionTypeName,
                    OriginalPath = originalPath,
                    TargetPath = postPath,
                    PreActionHash = preActionHash,
                    PostActionHash = postActionHash,
                    Status = "Completed",
                    Details = details
                });
            }
            else
            {
                await _rollbackEngine.RecordOperationAsync(new OperationJournalEntry
                {
                    BatchId = effectiveBatchId,
                    RuleId = ruleId,
                    RuleName = ruleName,
                    RuleRevisionId = ruleRevisionId,
                    ActionType = actionTypeName,
                    OriginalPath = originalPath,
                    TargetPath = result.TargetPath ?? result.ResultPath,
                    PreActionHash = preActionHash,
                    PostActionHash = null,
                    Status = "Failed",
                    Details = result.ErrorMessage
                });
            }
        }

        return result;
    }

    public async Task<ActionResult> ExecutePlannedActionAsync(
        PlannedFileAction plannedAction,
        Guid? batchId = null)
    {
        ArgumentNullException.ThrowIfNull(plannedAction);

        if (plannedAction.IsSkipped)
        {
            return new ActionResult
            {
                Success = false,
                Type = ActionResultType.Skipped,
                ErrorMessage = plannedAction.ResolutionNote ?? "Action skipped by policy."
            };
        }

        var fileInfo = new FileInfo(plannedAction.SourcePath);
        if (!fileInfo.Exists)
        {
            return new ActionResult
            {
                Success = false,
                Type = ActionResultType.Error,
                ErrorMessage = $"Source file '{plannedAction.SourcePath}' not found."
            };
        }

        // Verify confinement if TargetPath is set
        if (!string.IsNullOrEmpty(plannedAction.TargetPath))
        {
            string? destinationBase = plannedAction.FileAction switch
            {
                MoveFileAction m => variableEngine.Resolve(m.DestinationPath, fileInfo, plannedAction.Order),
                CopyFileAction c => variableEngine.Resolve(c.DestinationPath, fileInfo, plannedAction.Order),
                _ => null
            };

            if (!string.IsNullOrEmpty(destinationBase))
            {
                var fullBase = Path.GetFullPath(destinationBase).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                var fullTarget = Path.GetFullPath(plannedAction.TargetPath);
                if (!fullTarget.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
                {
                    return new ActionResult
                    {
                        Success = false,
                        Type = ActionResultType.Error,
                        ErrorMessage = $"Target path '{plannedAction.TargetPath}' escapes base destination '{destinationBase}' confinement."
                    };
                }
            }
        }

        var originalPath = fileInfo.FullName;
        var preActionHash = FileChecksumHelper.ComputeSha256(originalPath);

        if (plannedAction.FileAction is MoveFileAction moveAct && string.IsNullOrEmpty(moveAct.MonitoredSourceRoot))
        {
            moveAct.MonitoredSourceRoot = plannedAction.MonitoredSourceRoot;
        }
        if (plannedAction.FileAction is DeleteFileAction delAct && string.IsNullOrEmpty(delAct.MonitoredSourceRoot))
        {
            delAct.MonitoredSourceRoot = plannedAction.MonitoredSourceRoot;
        }

        ActionResult result = plannedAction.FileAction switch
        {
            MoveFileAction move => await ExecuteMoveAsync(move, fileInfo, plannedAction.PlannedCounter, plannedAction.TargetPath),
            CopyFileAction copy => await ExecuteCopyAsync(copy, fileInfo, plannedAction.PlannedCounter, plannedAction.TargetPath),
            RenameFileAction rename => await ExecuteRenameAsync(rename, fileInfo, plannedAction.PlannedCounter, plannedAction.TargetPath),
            ChangeExtensionAction changeExt => await ExecuteChangeExtensionAsync(changeExt, fileInfo, plannedAction.TargetPath),
            DeleteFileAction delete => await ExecuteDeleteAsync(
                delete,
                fileInfo,
                isAuthorizedForPermanentDeletion: plannedAction.AuthorizationContext != null
                    ? plannedAction.AuthorizationContext.IsPermanentDeleteAuthorized(plannedAction.SourcePath)
                    : plannedAction.IsAuthorizedForPermanentDeletion),
            ExtractArchiveAction extract => await ExecuteExtractArchiveAsync(
                extract,
                fileInfo,
                plannedAction.PlannedCounter,
                isAuthorizedForPermanentDeletion: plannedAction.AuthorizationContext != null
                    ? plannedAction.AuthorizationContext.IsPermanentDeleteAuthorized(plannedAction.SourcePath)
                    : plannedAction.IsAuthorizedForPermanentDeletion),
            _ => await ExecuteActionAsync(plannedAction.FileAction, fileInfo, plannedAction.PlannedCounter, batchId, plannedAction.RuleId, plannedAction.RuleName, plannedAction.RuleRevisionId)
        };

        if (_rollbackEngine is not null && (plannedAction.FileAction is MoveFileAction || plannedAction.FileAction is CopyFileAction || plannedAction.FileAction is RenameFileAction || plannedAction.FileAction is ChangeExtensionAction || plannedAction.FileAction is DeleteFileAction))
        {
            var effectiveBatchId = batchId ?? Guid.NewGuid();
            var actionTypeName = plannedAction.FileAction switch
            {
                MoveFileAction => "Move",
                CopyFileAction => "Copy",
                RenameFileAction => "Rename",
                ChangeExtensionAction => "ChangeExtension",
                DeleteFileAction => "Delete",
                _ => plannedAction.FileAction.GetType().Name
            };
            if (result.Success)
            {
                var postPath = result.TargetPath ?? result.ResultPath ?? originalPath;
                var postActionHash = !string.IsNullOrEmpty(postPath) && File.Exists(postPath) ? FileChecksumHelper.ComputeSha256(postPath) : null;
                var details = result.DisplacedBackupPath is not null
                    ? $"Displaced file backed up to: {result.DisplacedBackupPath}"
                    : null;

                await _rollbackEngine.RecordOperationAsync(new OperationJournalEntry
                {
                    BatchId = effectiveBatchId,
                    RuleId = plannedAction.RuleId,
                    RuleName = plannedAction.RuleName,
                    RuleRevisionId = plannedAction.RuleRevisionId,
                    ActionType = actionTypeName,
                    OriginalPath = originalPath,
                    TargetPath = postPath,
                    PreActionHash = preActionHash,
                    PostActionHash = postActionHash,
                    Status = "Completed",
                    Details = details
                });
            }
            else
            {
                await _rollbackEngine.RecordOperationAsync(new OperationJournalEntry
                {
                    BatchId = effectiveBatchId,
                    RuleId = plannedAction.RuleId,
                    RuleName = plannedAction.RuleName,
                    RuleRevisionId = plannedAction.RuleRevisionId,
                    ActionType = actionTypeName,
                    OriginalPath = originalPath,
                    TargetPath = result.TargetPath ?? result.ResultPath,
                    PreActionHash = preActionHash,
                    PostActionHash = null,
                    Status = "Failed",
                    Details = result.ErrorMessage
                });
            }
        }

        return result;
    }

    private async Task<ActionResult> ExecuteMoveAsync(
        MoveFileAction action,
        FileInfo fileInfo,
        int? counter,
        string? destinationOverride = null)
    {
        string? fullDestPath = null;
        string? displacedBackupPath = null;
        try
        {
            if (_fileLockDetector.IsTemporaryOrIncompleteFile(fileInfo.FullName))
            {
                return new ActionResult
                {
                    Success = false,
                    Type = ActionResultType.Skipped,
                    ErrorMessage = $"File '{fileInfo.FullName}' is an incomplete download or temporary file."
                };
            }

            if (!_fileLockDetector.IsFileReady(fileInfo.FullName))
            {
                var isReady = await _fileLockDetector.WaitForFileReadyAsync(
                    fileInfo.FullName,
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromMilliseconds(100));

                if (!isReady)
                {
                    return new ActionResult
                    {
                        Success = false,
                        Type = ActionResultType.Error,
                        ErrorMessage = $"File '{fileInfo.FullName}' is locked by another process."
                    };
                }
            }


            if (!string.IsNullOrEmpty(destinationOverride))
            {
                fullDestPath = destinationOverride;
                var parentDir = Path.GetDirectoryName(fullDestPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                if (File.Exists(fullDestPath))
                {
                    var (resolvedDest, displacedPath) = await ResolveConflictAsync(fullDestPath, action.ConflictResolution);
                    if (resolvedDest is null)
                    {
                        return new ActionResult
                        {
                            Success = false,
                            Type = action.ConflictResolution == ConflictResolution.Prompt ? ActionResultType.Warning : ActionResultType.Skipped,
                            ErrorMessage = action.ConflictResolution == ConflictResolution.Prompt
                                ? "Interactive prompt conflict resolution not supported in automated batch; operation skipped."
                                : "Operation skipped due to conflict"
                        };
                    }
                    fullDestPath = resolvedDest;
                    displacedBackupPath = displacedPath;
                }
            }
            else
            {
                var destPath = variableEngine.Resolve(action.DestinationPath, fileInfo, counter);
                if (action.PreserveSubfolderStructure && !string.IsNullOrWhiteSpace(action.MonitoredSourceRoot) && fileInfo.DirectoryName != null)
                {
                    var relSub = Simulation.ExecutionPlanGenerator.GetSafeRelativeSubfolder(action.MonitoredSourceRoot, fileInfo.DirectoryName);
                    if (!string.IsNullOrEmpty(relSub))
                    {
                        destPath = Path.Combine(destPath, relSub);
                    }
                }

                var destFileName = fileInfo.Name;
                fullDestPath = Path.Combine(destPath, destFileName);

                // Ensure destination directory exists
                Directory.CreateDirectory(destPath);

                // Handle conflicts
                var (resolvedDest, displacedPath) = await ResolveConflictAsync(fullDestPath, action.ConflictResolution);
                if (resolvedDest is null)
                {
                    return new ActionResult
                    {
                        Success = false,
                        Type = action.ConflictResolution == ConflictResolution.Prompt ? ActionResultType.Warning : ActionResultType.Skipped,
                        ErrorMessage = action.ConflictResolution == ConflictResolution.Prompt
                            ? "Interactive prompt conflict resolution not supported in automated batch; operation skipped."
                            : "Operation skipped due to conflict"
                    };
                }
                fullDestPath = resolvedDest;
                displacedBackupPath = displacedPath;
            }

            // Retry logic for locked files
            await RetryAsync(async () =>
            {
                await Task.Run(() => File.Move(fileInfo.FullName, fullDestPath));
            });

            // Remove empty source folder if requested
            if (action.RemoveEmptyFolders)
                await RemoveEmptyFoldersAsync(fileInfo.Directory, action.MonitoredSourceRoot);

            return new ActionResult { Success = true, ResultPath = fullDestPath, TargetPath = fullDestPath, DisplacedBackupPath = displacedBackupPath, Type = ActionResultType.Success };
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(displacedBackupPath) && File.Exists(displacedBackupPath) && !File.Exists(fullDestPath))
            {
                try { File.Move(displacedBackupPath, fullDestPath); } catch { }
            }
            return new ActionResult { Success = false, ErrorMessage = ex.Message, Type = ActionResultType.Error };
        }
    }

    private async Task<ActionResult> ExecuteCopyAsync(
        CopyFileAction action,
        FileInfo fileInfo,
        int? counter,
        string? destinationOverride = null)
    {
        string? fullDestPath = null;
        string? displacedBackupPath = null;
        try
        {
            if (_fileLockDetector.IsTemporaryOrIncompleteFile(fileInfo.FullName))
            {
                return new ActionResult
                {
                    Success = false,
                    Type = ActionResultType.Skipped,
                    ErrorMessage = $"File '{fileInfo.FullName}' is an incomplete download or temporary file."
                };
            }

            if (!_fileLockDetector.IsFileReady(fileInfo.FullName))
            {
                var isReady = await _fileLockDetector.WaitForFileReadyAsync(
                    fileInfo.FullName,
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromMilliseconds(100));

                if (!isReady)
                {
                    return new ActionResult
                    {
                        Success = false,
                        Type = ActionResultType.Error,
                        ErrorMessage = $"File '{fileInfo.FullName}' is locked by another process."
                    };
                }
            }


            if (!string.IsNullOrEmpty(destinationOverride))
            {
                fullDestPath = destinationOverride;
                var parentDir = Path.GetDirectoryName(fullDestPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }

                if (File.Exists(fullDestPath))
                {
                    var (resolvedDest, displacedPath) = await ResolveConflictAsync(fullDestPath, action.ConflictResolution);
                    if (resolvedDest is null)
                    {
                        return new ActionResult
                        {
                            Success = false,
                            Type = action.ConflictResolution == ConflictResolution.Prompt ? ActionResultType.Warning : ActionResultType.Skipped,
                            ErrorMessage = action.ConflictResolution == ConflictResolution.Prompt
                                ? "Interactive prompt conflict resolution not supported in automated batch; operation skipped."
                                : "Operation skipped due to conflict"
                        };
                    }
                    fullDestPath = resolvedDest;
                    displacedBackupPath = displacedPath;
                }
            }
            else
            {
                var destPath = variableEngine.Resolve(action.DestinationPath, fileInfo, counter);
                if (action.PreserveSubfolderStructure && !string.IsNullOrWhiteSpace(action.MonitoredSourceRoot) && fileInfo.DirectoryName != null)
                {
                    var relSub = Simulation.ExecutionPlanGenerator.GetSafeRelativeSubfolder(action.MonitoredSourceRoot, fileInfo.DirectoryName);
                    if (!string.IsNullOrEmpty(relSub))
                    {
                        destPath = Path.Combine(destPath, relSub);
                    }
                }

                var destFileName = fileInfo.Name;
                fullDestPath = Path.Combine(destPath, destFileName);

                // Ensure destination directory exists
                Directory.CreateDirectory(destPath);

                // Handle conflicts
                var (resolvedDest, displacedPath) = await ResolveConflictAsync(fullDestPath, action.ConflictResolution);
                if (resolvedDest is null)
                {
                    return new ActionResult
                    {
                        Success = false,
                        Type = action.ConflictResolution == ConflictResolution.Prompt ? ActionResultType.Warning : ActionResultType.Skipped,
                        ErrorMessage = action.ConflictResolution == ConflictResolution.Prompt
                            ? "Interactive prompt conflict resolution not supported in automated batch; operation skipped."
                            : "Operation skipped due to conflict"
                    };
                }
                fullDestPath = resolvedDest;
                displacedBackupPath = displacedPath;
            }

            // Retry logic
            await RetryAsync(async () =>
            {
                await Task.Run(() => File.Copy(fileInfo.FullName, fullDestPath, overwrite: true));
            });

            // Return the appropriate path for next action
            var resultPath = action.ApplyToSourceFile ? fileInfo.FullName : fullDestPath;

            return new ActionResult { Success = true, ResultPath = resultPath, TargetPath = fullDestPath, DisplacedBackupPath = displacedBackupPath, Type = ActionResultType.Success };
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(displacedBackupPath) && File.Exists(displacedBackupPath) && !File.Exists(fullDestPath))
            {
                try { File.Move(displacedBackupPath, fullDestPath); } catch { }
            }
            return new ActionResult { Success = false, ErrorMessage = ex.Message, Type = ActionResultType.Error };
        }
    }

    private async Task<ActionResult> ExecuteRenameAsync(
        RenameFileAction action,
        FileInfo fileInfo,
        int? counter,
        string? targetPathOverride = null)
    {
        string? newPath = null;
        string? displacedBackupPath = null;
        try
        {
            if (_fileLockDetector.IsTemporaryOrIncompleteFile(fileInfo.FullName))
            {
                return new ActionResult
                {
                    Success = false,
                    Type = ActionResultType.Skipped,
                    ErrorMessage = $"File '{fileInfo.FullName}' is an incomplete download or temporary file."
                };
            }

            if (!_fileLockDetector.IsFileReady(fileInfo.FullName))
            {
                var isReady = await _fileLockDetector.WaitForFileReadyAsync(
                    fileInfo.FullName,
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromMilliseconds(100));

                if (!isReady)
                {
                    return new ActionResult
                    {
                        Success = false,
                        Type = ActionResultType.Error,
                        ErrorMessage = $"File '{fileInfo.FullName}' is locked by another process."
                    };
                }
            }


            if (!string.IsNullOrEmpty(targetPathOverride))
            {
                newPath = targetPathOverride;
                var parentDir = Path.GetDirectoryName(newPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }
            }
            else
            {
                var newName = variableEngine.Resolve(action.NamePattern, fileInfo, counter);

                // Add extension if not included
                if (!Path.HasExtension(newName))
                    newName += fileInfo.Extension;

                newPath = Path.Combine(fileInfo.DirectoryName ?? string.Empty, newName);
            }

            // Handle conflicts
            var (resolvedPath, displaced) = await ResolveConflictAsync(newPath, action.ConflictResolution);

            displacedBackupPath = displaced;
            if (resolvedPath is null)
            {
                return new ActionResult
                {
                    Success = false,
                    Type = action.ConflictResolution == ConflictResolution.Prompt ? ActionResultType.Warning : ActionResultType.Skipped,
                    ErrorMessage = action.ConflictResolution == ConflictResolution.Prompt
                        ? "Interactive prompt conflict resolution not supported in automated batch; operation skipped."
                        : "Operation skipped due to conflict"
                };
            }
            newPath = resolvedPath;

            // Retry logic
            await RetryAsync(async () =>
            {
                await Task.Run(() => File.Move(fileInfo.FullName, newPath));
            });

            return new ActionResult { Success = true, ResultPath = newPath, TargetPath = newPath, DisplacedBackupPath = displacedBackupPath, Type = ActionResultType.Success };
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(displacedBackupPath) && File.Exists(displacedBackupPath) && !File.Exists(newPath))
            {
                try { File.Move(displacedBackupPath, newPath); } catch { }
            }
            return new ActionResult { Success = false, ErrorMessage = ex.Message, Type = ActionResultType.Error };
        }
    }

    private async Task<ActionResult> ExecuteChangeExtensionAsync(
        ChangeExtensionAction action,
        FileInfo fileInfo,
        string? targetPathOverride = null)
    {
        string? newPath = null;
        string? displacedBackupPath = null;
        try
        {
            if (_fileLockDetector.IsTemporaryOrIncompleteFile(fileInfo.FullName))
            {
                return new ActionResult
                {
                    Success = false,
                    Type = ActionResultType.Skipped,
                    ErrorMessage = $"File '{fileInfo.FullName}' is an incomplete download or temporary file."
                };
            }

            if (!_fileLockDetector.IsFileReady(fileInfo.FullName))
            {
                var isReady = await _fileLockDetector.WaitForFileReadyAsync(
                    fileInfo.FullName,
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromMilliseconds(100));

                if (!isReady)
                {
                    return new ActionResult
                    {
                        Success = false,
                        Type = ActionResultType.Error,
                        ErrorMessage = $"File '{fileInfo.FullName}' is locked by another process."
                    };
                }
            }


            if (!string.IsNullOrEmpty(targetPathOverride))
            {
                newPath = targetPathOverride;
                var parentDir = Path.GetDirectoryName(newPath);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    Directory.CreateDirectory(parentDir);
                }
            }
            else
            {
                var newExtension = action.NewExtension.TrimStart('.');
                var nameWithoutExt = Path.GetFileNameWithoutExtension(fileInfo.Name);
                newPath = Path.Combine(fileInfo.DirectoryName ?? string.Empty, $"{nameWithoutExt}.{newExtension}");
            }

            // Handle conflicts
            var (resolvedPath, displaced) = await ResolveConflictAsync(newPath, action.ConflictResolution);

            displacedBackupPath = displaced;
            if (resolvedPath is null)
            {
                return new ActionResult
                {
                    Success = false,
                    Type = action.ConflictResolution == ConflictResolution.Prompt ? ActionResultType.Warning : ActionResultType.Skipped,
                    ErrorMessage = action.ConflictResolution == ConflictResolution.Prompt
                        ? "Interactive prompt conflict resolution not supported in automated batch; operation skipped."
                        : "Operation skipped due to conflict"
                };
            }
            newPath = resolvedPath;

            // Retry logic
            await RetryAsync(async () =>
            {
                await Task.Run(() => File.Move(fileInfo.FullName, newPath));
            });

            return new ActionResult { Success = true, ResultPath = newPath, TargetPath = newPath, DisplacedBackupPath = displacedBackupPath, Type = ActionResultType.Success };
        }
        catch (Exception ex)
        {
            if (!string.IsNullOrEmpty(displacedBackupPath) && File.Exists(displacedBackupPath) && !File.Exists(newPath))
            {
                try { File.Move(displacedBackupPath, newPath); } catch { }
            }
            return new ActionResult { Success = false, ErrorMessage = ex.Message, Type = ActionResultType.Error };
        }
    }

    private async Task<ActionResult> ExecuteDeleteAsync(
        DeleteFileAction action,
        FileInfo fileInfo,
        bool isAuthorizedForPermanentDeletion = false)
    {
        try
        {
            if (!_fileLockDetector.IsFileReady(fileInfo.FullName))
            {
                var isReady = await _fileLockDetector.WaitForFileReadyAsync(
                    fileInfo.FullName,
                    TimeSpan.FromMilliseconds(500),
                    TimeSpan.FromMilliseconds(100));

                if (!isReady)
                {
                    return new ActionResult
                    {
                        Success = false,
                        Type = ActionResultType.Error,
                        ErrorMessage = $"File '{fileInfo.FullName}' is locked by another process."
                    };
                }
            }

            // allowPermanentFallback is granted if permanent deletion is configured and explicitly authorized by user confirmation.
            // ConfirmBeforeDelete is strictly a user preference determining whether to prompt, NOT an execution authorization token.
            bool allowPermanentFallback = !action.UseRecycleBin && isAuthorizedForPermanentDeletion;

            await RetryAsync(async () =>
            {
                await _safeFileSystem.DeleteFileSafelyAsync(
                    fileInfo.FullName,
                    useRecycleBin: action.UseRecycleBin,
                    allowPermanentFallback: allowPermanentFallback);
            });

            // Remove empty parent folders if requested
            if (action.RemoveEmptyFolders)
                await RemoveEmptyFoldersAsync(fileInfo.Directory, action.MonitoredSourceRoot);

            return new ActionResult { Success = true, Type = ActionResultType.Success };
        }
        catch (RecycleBinUnavailableException ex)
        {
            return new ActionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Type = ActionResultType.Error
            };
        }
        catch (Exception ex)
        {
            return new ActionResult { Success = false, ErrorMessage = ex.Message, Type = ActionResultType.Error };
        }
    }

    private async Task<ActionResult> ExecuteExtractArchiveAsync(
        ExtractArchiveAction action,
        FileInfo fileInfo,
        int? counter,
        bool isAuthorizedForPermanentDeletion = false)
    {
        try
        {
            var destPath = variableEngine.Resolve(action.DestinationPath, fileInfo, counter);

            // If no destination specified, extract to same folder
            if (string.IsNullOrWhiteSpace(destPath))
                destPath = fileInfo.DirectoryName ?? string.Empty;

            // Ensure destination directory exists
            Directory.CreateDirectory(destPath);

            await Task.Run(() =>
            {
                using var archive = ArchiveFactory.OpenArchive(fileInfo.FullName, new ReaderOptions());
                var options = new ExtractionOptions
                {
                    ExtractFullPath = true,
                    Overwrite = action.OverwriteExisting
                };

                foreach (var entry in archive.Entries.Where(e => !e.IsDirectory))
                {
                    entry.WriteToDirectory(destPath, options);
                }
            });

            // Delete archive if requested safely via SafeFileSystem
            if (action.DeleteAfterExtraction)
            {
                bool allowPermanentFallback = !action.UseRecycleBinForArchiveDeletion && isAuthorizedForPermanentDeletion;
                await _safeFileSystem.DeleteFileSafelyAsync(
                    fileInfo.FullName,
                    useRecycleBin: action.UseRecycleBinForArchiveDeletion,
                    allowPermanentFallback: allowPermanentFallback);
            }

            return new ActionResult { Success = true, ResultPath = destPath, TargetPath = destPath, Type = ActionResultType.Success };
        }
        catch (Exception ex)
        {
            return new ActionResult
            {
                Success = false,
                ErrorMessage = $"Failed to extract archive: {ex.Message}",
                Type = ActionResultType.Error
            };
        }
    }

    private async Task<ActionResult> ExecuteRunCommandAsync(RunCommandAction action, FileInfo fileInfo, int? counter)
    {
        try
        {
            var command = variableEngine.Resolve(action.Command, fileInfo, counter);
            var workingDir = string.IsNullOrWhiteSpace(action.WorkingDirectory)
                ? fileInfo.DirectoryName
                : variableEngine.Resolve(action.WorkingDirectory, fileInfo, counter);

            // Validate command to prevent injection
            if (string.IsNullOrWhiteSpace(command))
            {
                return new ActionResult
                {
                    Success = false,
                    ErrorMessage = "Command cannot be empty",
                    Type = ActionResultType.Error
                };
            }

            // Block dangerous shell metacharacters that could chain commands
            string[] dangerousPatterns = ["&&", "||", "|", ";", "`", "$(", "%COMSPEC%", "%SystemRoot%"];
            foreach (var pattern in dangerousPatterns)
            {
                if (command.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return new ActionResult
                    {
                        Success = false,
                        ErrorMessage = $"Command contains disallowed characters: {pattern}",
                        Type = ActionResultType.Error
                    };
                }
            }

            // Validate working directory path
            if (!string.IsNullOrWhiteSpace(workingDir) && !Directory.Exists(workingDir))
            {
                return new ActionResult
                {
                    Success = false,
                    ErrorMessage = $"Working directory does not exist: {workingDir}",
                    Type = ActionResultType.Error
                };
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                WorkingDirectory = workingDir,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            if (action.WaitForCompletion)
            {
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                var completed = await Task.Run(() =>
                    process.WaitForExit(action.TimeoutSeconds * 1000));

                if (!completed)
                {
                    process.Kill(entireProcessTree: true);
                    return new ActionResult
                    {
                        Success = false,
                        ErrorMessage = $"Command timed out after {action.TimeoutSeconds} seconds",
                        Type = ActionResultType.Error
                    };
                }

                _ = await outputTask;
                var error = await errorTask;

                if (process.ExitCode != 0)
                {
                    return new ActionResult
                    {
                        Success = false,
                        ErrorMessage = $"Command failed with exit code {process.ExitCode}: {error}",
                        Type = ActionResultType.Error
                    };
                }
            }

            return new ActionResult { Success = true, ResultPath = fileInfo.FullName, Type = ActionResultType.Success };
        }
        catch (Exception ex)
        {
            return new ActionResult
            {
                Success = false,
                ErrorMessage = $"Failed to execute command: {ex.Message}",
                Type = ActionResultType.Error
            };
        }
    }

    private static async Task<(string? ResolvedPath, string? DisplacedBackupPath)> ResolveConflictAsync(string destinationPath, ConflictResolution strategy)
    {
        if (!File.Exists(destinationPath))
            return (destinationPath, null);

        switch (strategy)
        {
            case ConflictResolution.Skip:
                return (null, null);
            case ConflictResolution.Overwrite:
            {
                var directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
                var fileNameWithoutExt = Path.GetFileNameWithoutExtension(destinationPath);
                var extension = Path.GetExtension(destinationPath);
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                var backupPath = Path.Combine(directory, $"{fileNameWithoutExt}_displaced_{timestamp}{extension}");
                await RetryAsync(async () => await Task.Run(() => File.Move(destinationPath, backupPath)));
                return (destinationPath, backupPath);
            }
            case ConflictResolution.RenameNew:
                return (GenerateUniquePath(destinationPath), null);
            case ConflictResolution.RenameOld:
            {
                var renamed = await RenameExistingFileAsync(destinationPath);
                return (renamed.DestinationPath, renamed.BackupPath);
            }
            case ConflictResolution.Prompt:
            default:
                return (null, null);
        }
    }

    private static string GenerateUniquePath(string originalPath)
    {
        var directory = Path.GetDirectoryName(originalPath) ?? string.Empty;
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
        var extension = Path.GetExtension(originalPath);

        int counter = 1;
        string newPath;

        do
        {
            newPath = Path.Combine(directory, $"{fileNameWithoutExt}({counter}){extension}");
            counter++;
        }
        while (File.Exists(newPath));

        return newPath;
    }

    private static async Task<(string DestinationPath, string BackupPath)> RenameExistingFileAsync(string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath) ?? string.Empty;
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(destinationPath);
        var extension = Path.GetExtension(destinationPath);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        var backupPath = Path.Combine(directory, $"{fileNameWithoutExt}_backup_{timestamp}{extension}");

        await RetryAsync(async () =>
        {
            await Task.Run(() => File.Move(destinationPath, backupPath));
        });

        return (destinationPath, backupPath);
    }

    private static readonly HashSet<string> ProtectedFolders = InitializeProtectedFolders();

    private static HashSet<string> InitializeProtectedFolders()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddIfValid(string? path)
        {
            if (!string.IsNullOrWhiteSpace(path))
            {
                try
                {
                    paths.Add(Path.GetFullPath(path).TrimEnd('\\', '/'));
                }
                catch { }
            }
        }

        AddIfValid(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        AddIfValid(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        AddIfValid(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
        AddIfValid(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));
        AddIfValid(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        AddIfValid(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles));
        AddIfValid(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        AddIfValid(Environment.GetFolderPath(Environment.SpecialFolder.System));

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            AddIfValid(Path.Combine(userProfile, "Downloads"));
            AddIfValid(Path.Combine(userProfile, "Documents"));
            AddIfValid(Path.Combine(userProfile, "Pictures"));
            AddIfValid(Path.Combine(userProfile, "Music"));
            AddIfValid(Path.Combine(userProfile, "Videos"));
            AddIfValid(Path.Combine(userProfile, "Desktop"));
        }

        return paths;
    }

    private static async Task RemoveEmptyFoldersAsync(DirectoryInfo? directory, string? stopBoundaryPath = null)
    {
        if (directory is null || !directory.Exists)
            return;

        try
        {
            var normalizedDir = Path.GetFullPath(directory.FullName).TrimEnd('\\', '/');

            // Never delete root drive (e.g. C:\) or any protected system/user directory
            if (directory.Parent is null || ProtectedFolders.Contains(normalizedDir))
                return;

            // If a stop boundary path is specified, strictly clamp to prevent deleting the boundary itself or anything above it
            if (!string.IsNullOrWhiteSpace(stopBoundaryPath))
            {
                var normalizedBoundary = Path.GetFullPath(stopBoundaryPath).TrimEnd('\\', '/');
                if (string.Equals(normalizedDir, normalizedBoundary, StringComparison.OrdinalIgnoreCase) ||
                    !normalizedDir.StartsWith(normalizedBoundary + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            // Don't delete if contains files or subdirectories
            if (directory.GetFiles().Length > 0 || directory.GetDirectories().Length > 0)
                return;

            await Task.Run(() => directory.Delete());

            // Recurse to parent only if parent is not root or protected
            if (directory.Parent is not null)
            {
                await RemoveEmptyFoldersAsync(directory.Parent, stopBoundaryPath);
            }
        }
        catch
        {
            // Silently fail - not critical
        }
    }

    private static async Task RetryAsync(Func<Task> action)
    {
        Exception? lastException = null;

        for (int i = 0; i < MaxRetries; i++)
        {
            try
            {
                await action();
                return;
            }
            catch (IOException ex) when (i < MaxRetries - 1)
            {
                lastException = ex;
                await Task.Delay(RetryDelays[i]);
            }
        }

        if (lastException is not null)
            throw lastException;
    }

    public async Task<List<ActionPreview>> PreviewActionsAsync(List<FileAction> actions, FileInfo fileInfo, int? counter = null)
    {
        var previews = new List<ActionPreview>();
        var currentPath = fileInfo.FullName;

        foreach (var action in actions.OrderBy(a => a.Order))
        {
            var preview = action switch
            {
                MoveFileAction move => PreviewMove(move, currentPath, counter, fileInfo),
                CopyFileAction copy => PreviewCopy(copy, currentPath, counter, fileInfo),
                RenameFileAction rename => PreviewRename(rename, currentPath, counter, fileInfo),
                ChangeExtensionAction changeExt => PreviewChangeExtension(changeExt, currentPath),
                DeleteFileAction delete => PreviewDelete(delete),
                ExtractArchiveAction extract => PreviewExtractArchive(extract, currentPath, counter, fileInfo),
                RunCommandAction runCmd => PreviewRunCommand(runCmd, currentPath, counter, fileInfo),
                _ => new ActionPreview { ActionType = "Unknown", Description = "Unknown action type" }
            };

            previews.Add(preview);

            // Update current path for next action if this action would change it
            if (!string.IsNullOrEmpty(preview.ResultPath))
                currentPath = preview.ResultPath;
        }

        return await Task.FromResult(previews);
    }

    private ActionPreview PreviewMove(MoveFileAction action, string currentPath, int? counter, FileInfo fileInfo)
    {
        var currentName = Path.GetFileName(currentPath);
        var destPath = variableEngine.Resolve(action.DestinationPath, fileInfo, counter);
        var fullDestPath = Path.Combine(destPath, currentName);
        var hasConflict = File.Exists(fullDestPath);

        return new ActionPreview
        {
            ActionType = "Move",
            Description = $"Move to: {destPath}",
            ResultPath = fullDestPath,
            HasConflict = hasConflict,
            ConflictResolution = hasConflict ? action.ConflictResolution.ToString() : null
        };
    }

    private ActionPreview PreviewCopy(CopyFileAction action, string currentPath, int? counter, FileInfo fileInfo)
    {
        var currentName = Path.GetFileName(currentPath);
        var destPath = variableEngine.Resolve(action.DestinationPath, fileInfo, counter);
        var fullDestPath = Path.Combine(destPath, currentName);
        var hasConflict = File.Exists(fullDestPath);

        return new ActionPreview
        {
            ActionType = "Copy",
            Description = $"Copy to: {destPath}",
            ResultPath = action.ApplyToSourceFile ? currentPath : fullDestPath,
            HasConflict = hasConflict,
            ConflictResolution = hasConflict ? action.ConflictResolution.ToString() : null
        };
    }

    private ActionPreview PreviewRename(RenameFileAction action, string currentPath, int? counter, FileInfo fileInfo)
    {
        var newName = variableEngine.Resolve(action.NamePattern, fileInfo, counter);
        if (!Path.HasExtension(newName))
            newName += fileInfo.Extension;

        var directory = Path.GetDirectoryName(currentPath) ?? string.Empty;
        var newPath = Path.Combine(directory, newName);
        var hasConflict = File.Exists(newPath) && newPath != currentPath;

        return new ActionPreview
        {
            ActionType = "Rename",
            Description = $"Rename to: {newName}",
            ResultPath = newPath,
            HasConflict = hasConflict,
            ConflictResolution = hasConflict ? action.ConflictResolution.ToString() : null
        };
    }

    private static ActionPreview PreviewChangeExtension(ChangeExtensionAction action, string currentPath)
    {
        var currentName = Path.GetFileName(currentPath);
        var newExt = action.NewExtension.StartsWith('.') ? action.NewExtension : "." + action.NewExtension;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(currentName);
        var newName = nameWithoutExt + newExt;
        var directory = Path.GetDirectoryName(currentPath) ?? string.Empty;
        var newPath = Path.Combine(directory, newName);
        var hasConflict = File.Exists(newPath) && newPath != currentPath;

        return new ActionPreview
        {
            ActionType = "Change Extension",
            Description = $"Change extension to: {newExt}",
            ResultPath = newPath,
            HasConflict = hasConflict,
            ConflictResolution = hasConflict ? action.ConflictResolution.ToString() : null
        };
    }

    private static ActionPreview PreviewDelete(DeleteFileAction action)
    {
        return new ActionPreview
        {
            ActionType = "Delete",
            Description = action.UseRecycleBin ? "Send to Recycle Bin" : "Permanently delete",
            ResultPath = null,
            HasConflict = false
        };
    }

    private ActionPreview PreviewExtractArchive(ExtractArchiveAction action, string currentPath, int? counter, FileInfo fileInfo)
    {
        var destPath = variableEngine.Resolve(action.DestinationPath, fileInfo, counter);
        if (string.IsNullOrWhiteSpace(destPath))
            destPath = Path.GetDirectoryName(currentPath) ?? string.Empty;

        return new ActionPreview
        {
            ActionType = "Extract Archive",
            Description = $"Extract to: {destPath}",
            ResultPath = destPath,
            HasConflict = false
        };
    }

    private ActionPreview PreviewRunCommand(RunCommandAction action, string currentPath, int? counter, FileInfo fileInfo)
    {
        var command = variableEngine.Resolve(action.Command, fileInfo, counter);
        return new ActionPreview
        {
            ActionType = "Run Command",
            Description = $"Execute: {command}",
            ResultPath = currentPath,
            HasConflict = false
        };
    }
}
