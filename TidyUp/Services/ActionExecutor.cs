using System.Diagnostics;
using System.IO;
using Microsoft.VisualBasic.FileIO;
using SharpCompress.Archives;
using SharpCompress.Common;
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

    public async Task<List<ActionResult>> ExecuteActionsAsync(
        List<FileAction> actions,
        FileInfo fileInfo,
        int? counter = null,
        Guid? batchId = null)
    {
        var effectiveBatchId = batchId ?? Guid.NewGuid();
        var results = new List<ActionResult>();
        var currentFile = fileInfo;

        foreach (var action in actions.OrderBy(a => a.Order))
        {                       
            var result = await ExecuteActionAsync(action, currentFile, counter, effectiveBatchId);
            results.Add(result);

            if (!result.Success)
                break;

            if (!string.IsNullOrEmpty(result.ResultPath) && File.Exists(result.ResultPath))
                currentFile = new FileInfo(result.ResultPath);
        }

        return results;
    }

    public async Task<ActionResult> ExecuteActionAsync(
        FileAction action,
        FileInfo fileInfo,
        int? counter = null,
        Guid? batchId = null)
    {
        var originalPath = fileInfo.FullName;
        var preActionHash = FileChecksumHelper.ComputeSha256(originalPath);

        var result = action switch
        {
            MoveFileAction move => await ExecuteMoveAsync(move, fileInfo, counter),
            CopyFileAction copy => await ExecuteCopyAsync(copy, fileInfo, counter),
            RenameFileAction rename => await ExecuteRenameAsync(rename, fileInfo, counter),
            ChangeExtensionAction changeExt => await ExecuteChangeExtensionAsync(changeExt, fileInfo),
            DeleteFileAction delete => await ExecuteDeleteAsync(delete, fileInfo),
            ExtractArchiveAction extract => await ExecuteExtractArchiveAsync(extract, fileInfo, counter),
            RunCommandAction runCmd => await ExecuteRunCommandAsync(runCmd, fileInfo, counter),
            _ => new ActionResult { Success = false, ErrorMessage = "Unknown action type", Type = ActionResultType.Error }
        };

        if (result.Success && _rollbackEngine is not null)
        {
            var postPath = result.TargetPath ?? (action is DeleteFileAction ? null : result.ResultPath ?? originalPath);
            var postActionHash = !string.IsNullOrEmpty(postPath) ? FileChecksumHelper.ComputeSha256(postPath) : null;

            await _rollbackEngine.RecordOperationAsync(new OperationJournalEntry
            {
                BatchId = batchId ?? Guid.NewGuid(),
                ActionType = action switch
                {
                    MoveFileAction => "Move",
                    CopyFileAction => "Copy",
                    RenameFileAction => "Rename",
                    ChangeExtensionAction => "ChangeExtension",
                    DeleteFileAction => "Delete",
                    ExtractArchiveAction => "ExtractArchive",
                    RunCommandAction => "RunCommand",
                    _ => action.GetType().Name
                },
                OriginalPath = originalPath,
                TargetPath = postPath,
                PreActionHash = preActionHash,
                PostActionHash = postActionHash,
                Status = "Completed",
                Details = action is DeleteFileAction del
                    ? (del.UseRecycleBin ? "Sent to Recycle Bin" : "Permanently Deleted")
                    : null
            });
        }

        return result;
    }

    private async Task<ActionResult> ExecuteMoveAsync(MoveFileAction action, FileInfo fileInfo, int? counter)
    {
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

            var destPath = variableEngine.Resolve(action.DestinationPath, fileInfo, counter);
            var destFileName = fileInfo.Name;
            var fullDestPath = Path.Combine(destPath, destFileName);

            // Ensure destination directory exists
            Directory.CreateDirectory(destPath);

            // Handle conflicts
            fullDestPath = await ResolveConflictAsync(fullDestPath, action.ConflictResolution);
            if (fullDestPath is null)
            {
                return new ActionResult
                {
                    Success = false,
                    Type = ActionResultType.Skipped,
                    ErrorMessage = "Operation skipped due to conflict"
                };
            }

            // If overwriting, delete the destination file first since File.Move doesn't support overwrite
            if (action.ConflictResolution == ConflictResolution.Overwrite && File.Exists(fullDestPath))
                await Task.Run(() => File.Delete(fullDestPath));

            // Retry logic for locked files
            await RetryAsync(async () =>
            {
                await Task.Run(() => File.Move(fileInfo.FullName, fullDestPath));
            });

            // Remove empty source folder if requested
            if (action.RemoveEmptyFolders)
                await RemoveEmptyFoldersAsync(fileInfo.Directory);

            return new ActionResult { Success = true, ResultPath = fullDestPath, TargetPath = fullDestPath, Type = ActionResultType.Success };
        }
        catch (Exception ex)
        {
            return new ActionResult { Success = false, ErrorMessage = ex.Message, Type = ActionResultType.Error };
        }
    }

    private async Task<ActionResult> ExecuteCopyAsync(CopyFileAction action, FileInfo fileInfo, int? counter)
    {
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

            var destPath = variableEngine.Resolve(action.DestinationPath, fileInfo, counter);
            var destFileName = fileInfo.Name;
            var fullDestPath = Path.Combine(destPath, destFileName);

            // Ensure destination directory exists
            Directory.CreateDirectory(destPath);

            // Handle conflicts
            fullDestPath = await ResolveConflictAsync(fullDestPath, action.ConflictResolution);
            if (fullDestPath is null)
            {
                return new ActionResult
                {
                    Success = false,
                    Type = ActionResultType.Skipped,
                    ErrorMessage = "Operation skipped due to conflict"
                };
            }

            // Retry logic
            await RetryAsync(async () =>
            {
                await Task.Run(() => File.Copy(fileInfo.FullName, fullDestPath, overwrite: true));
            });

            // Return the appropriate path for next action
            var resultPath = action.ApplyToSourceFile ? fileInfo.FullName : fullDestPath;

            return new ActionResult { Success = true, ResultPath = resultPath, TargetPath = fullDestPath, Type = ActionResultType.Success };
        }
        catch (Exception ex)
        {
            return new ActionResult { Success = false, ErrorMessage = ex.Message, Type = ActionResultType.Error };
        }
    }

    private async Task<ActionResult> ExecuteRenameAsync(RenameFileAction action, FileInfo fileInfo, int? counter)
    {
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

            var newName = variableEngine.Resolve(action.NamePattern, fileInfo, counter);

            // Add extension if not included
            if (!Path.HasExtension(newName))
                newName += fileInfo.Extension;

            var newPath = Path.Combine(fileInfo.DirectoryName ?? string.Empty, newName);

            // Handle conflicts
            newPath = await ResolveConflictAsync(newPath, action.ConflictResolution);
            if (newPath is null)
            {
                return new ActionResult
                {
                    Success = false,
                    Type = ActionResultType.Skipped,
                    ErrorMessage = "Operation skipped due to conflict"
                };
            }

            // If overwriting, delete the destination file first since File.Move doesn't support overwrite
            if (action.ConflictResolution == ConflictResolution.Overwrite && File.Exists(newPath))
                await Task.Run(() => File.Delete(newPath));

            // Retry logic
            await RetryAsync(async () =>
            {
                await Task.Run(() => File.Move(fileInfo.FullName, newPath));
            });

            return new ActionResult { Success = true, ResultPath = newPath, TargetPath = newPath, Type = ActionResultType.Success };
        }
        catch (Exception ex)
        {
            return new ActionResult { Success = false, ErrorMessage = ex.Message, Type = ActionResultType.Error };
        }
    }

    private async Task<ActionResult> ExecuteChangeExtensionAsync(ChangeExtensionAction action, FileInfo fileInfo)
    {
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

            var newExtension = action.NewExtension.TrimStart('.');
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileInfo.Name);
            var newPath = Path.Combine(fileInfo.DirectoryName ?? string.Empty, $"{nameWithoutExt}.{newExtension}");

            // Handle conflicts
            newPath = await ResolveConflictAsync(newPath, action.ConflictResolution);
            if (newPath is null)
            {
                return new ActionResult
                {
                    Success = false,
                    Type = ActionResultType.Skipped,
                    ErrorMessage = "Operation skipped due to conflict"
                };
            }

            // If overwriting, delete the destination file first since File.Move doesn't support overwrite
            if (action.ConflictResolution == ConflictResolution.Overwrite && File.Exists(newPath))
                await Task.Run(() => File.Delete(newPath));

            // Retry logic
            await RetryAsync(async () =>
            {
                await Task.Run(() => File.Move(fileInfo.FullName, newPath));
            });

            return new ActionResult { Success = true, ResultPath = newPath, TargetPath = newPath, Type = ActionResultType.Success };
        }
        catch (Exception ex)
        {
            return new ActionResult { Success = false, ErrorMessage = ex.Message, Type = ActionResultType.Error };
        }
    }

    private async Task<ActionResult> ExecuteDeleteAsync(DeleteFileAction action, FileInfo fileInfo)
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

            await RetryAsync(async () =>
            {
                await _safeFileSystem.DeleteFileSafelyAsync(
                    fileInfo.FullName,
                    useRecycleBin: action.UseRecycleBin,
                    allowPermanentFallback: !action.UseRecycleBin && !action.ConfirmBeforeDelete);
            });

            // Remove empty parent folders if requested
            if (action.RemoveEmptyFolders)
                await RemoveEmptyFoldersAsync(fileInfo.Directory);

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

    private async Task<ActionResult> ExecuteExtractArchiveAsync(ExtractArchiveAction action, FileInfo fileInfo, int? counter)
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
                using var archive = ArchiveFactory.Open(fileInfo.FullName);
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

            // Delete archive if requested
            if (action.DeleteAfterExtraction)
                await Task.Run(() => File.Delete(fileInfo.FullName));

            return new ActionResult { Success = true, ResultPath = destPath, Type = ActionResultType.Success };
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

    private static async Task<string?> ResolveConflictAsync(string destinationPath, ConflictResolution strategy)
    {
        if (!File.Exists(destinationPath))
            return destinationPath;

        return strategy switch
        {
            ConflictResolution.Skip => null,
            ConflictResolution.Overwrite => destinationPath,
            ConflictResolution.RenameNew => GenerateUniquePath(destinationPath),
            ConflictResolution.RenameOld => await RenameExistingFileAsync(destinationPath),
            ConflictResolution.Prompt => destinationPath, // TODO: Implement user prompt
            _ => null
        };
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

    private static async Task<string> RenameExistingFileAsync(string destinationPath)
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

        return destinationPath;
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
            var normalizedDir = directory.FullName.TrimEnd('\\', '/');

            // Never delete root drive (e.g. C:\) or any protected system/user directory
            if (directory.Parent is null || ProtectedFolders.Contains(normalizedDir))
                return;

            // If a stop boundary path is specified, do not delete the boundary folder itself
            if (!string.IsNullOrEmpty(stopBoundaryPath))
            {
                var normalizedBoundary = Path.GetFullPath(stopBoundaryPath).TrimEnd('\\', '/');
                if (string.Equals(normalizedDir, normalizedBoundary, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            // Don't delete if contains files or subdirectories
            if (directory.GetFiles().Length > 0 || directory.GetDirectories().Length > 0)
                return;

            await Task.Run(() => directory.Delete());

            // Recurse to parent only if parent is not root or protected
            if (directory.Parent is not null)
            {
                var parentNorm = directory.Parent.FullName.TrimEnd('\\', '/');
                if (!ProtectedFolders.Contains(parentNorm))
                {
                    if (string.IsNullOrEmpty(stopBoundaryPath) ||
                        !string.Equals(parentNorm, Path.GetFullPath(stopBoundaryPath).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase))
                    {
                        await RemoveEmptyFoldersAsync(directory.Parent, stopBoundaryPath);
                    }
                }
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
