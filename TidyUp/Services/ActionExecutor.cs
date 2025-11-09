using System.Diagnostics;
using System.IO;
using Microsoft.VisualBasic.FileIO;
using SharpCompress.Archives;
using SharpCompress.Common;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;

namespace TidyUp.Services;

/// <summary>
/// Implementation of file action executor with retry logic and conflict resolution.
/// </summary>
public class ActionExecutor : IActionExecutor
{
    private readonly IVariableEngine _variableEngine;
    private const int MaxRetries = 3;
    private static readonly int[] RetryDelays = { 100, 500, 2000 }; // milliseconds

    public ActionExecutor(IVariableEngine variableEngine)
    {
        _variableEngine = variableEngine;
    }

    public async Task<List<ActionResult>> ExecuteActionsAsync(List<FileAction> actions, FileInfo fileInfo, int? counter = null)
    {
        var results = new List<ActionResult>();
        var currentFile = fileInfo;

        foreach (var action in actions.OrderBy(a => a.Order))
        {
            var result = await ExecuteActionAsync(action, currentFile, counter);
            results.Add(result);

            if (!result.Success)
            {
                // Stop on error
                break;
            }

            // Update current file path if action changed it
            if (!string.IsNullOrEmpty(result.ResultPath) && File.Exists(result.ResultPath))
            {
                currentFile = new FileInfo(result.ResultPath);
            }
        }

        return results;
    }

    public async Task<ActionResult> ExecuteActionAsync(FileAction action, FileInfo fileInfo, int? counter = null)
    {
        return action switch
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
    }

    private async Task<ActionResult> ExecuteMoveAsync(MoveFileAction action, FileInfo fileInfo, int? counter)
    {
        try
        {
            var destPath = _variableEngine.Resolve(action.DestinationPath, fileInfo, counter);
            var destFileName = fileInfo.Name;
            var fullDestPath = Path.Combine(destPath, destFileName);

            // Ensure destination directory exists
            Directory.CreateDirectory(destPath);

            // Handle conflicts
            fullDestPath = await ResolveConflictAsync(fullDestPath, action.ConflictResolution);
            if (fullDestPath == null)
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
            {
                await Task.Run(() => File.Delete(fullDestPath));
            }

            // Retry logic for locked files
            await RetryAsync(async () =>
            {
                await Task.Run(() => File.Move(fileInfo.FullName, fullDestPath));
            });

            // Remove empty source folder if requested
            if (action.RemoveEmptyFolders)
            {
                await RemoveEmptyFoldersAsync(fileInfo.Directory);
            }

            return new ActionResult
            {
                Success = true,
                ResultPath = fullDestPath,
                Type = ActionResultType.Success
            };
        }
        catch (Exception ex)
        {
            return new ActionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Type = ActionResultType.Error
            };
        }
    }

    private async Task<ActionResult> ExecuteCopyAsync(CopyFileAction action, FileInfo fileInfo, int? counter)
    {
        try
        {
            var destPath = _variableEngine.Resolve(action.DestinationPath, fileInfo, counter);
            var destFileName = fileInfo.Name;
            var fullDestPath = Path.Combine(destPath, destFileName);

            // Ensure destination directory exists
            Directory.CreateDirectory(destPath);

            // Handle conflicts
            fullDestPath = await ResolveConflictAsync(fullDestPath, action.ConflictResolution);
            if (fullDestPath == null)
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

            return new ActionResult
            {
                Success = true,
                ResultPath = resultPath,
                Type = ActionResultType.Success
            };
        }
        catch (Exception ex)
        {
            return new ActionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Type = ActionResultType.Error
            };
        }
    }

    private async Task<ActionResult> ExecuteRenameAsync(RenameFileAction action, FileInfo fileInfo, int? counter)
    {
        try
        {
            var newName = _variableEngine.Resolve(action.NamePattern, fileInfo, counter);
            
            // Add extension if not included
            if (!Path.HasExtension(newName))
            {
                newName += fileInfo.Extension;
            }

            var newPath = Path.Combine(fileInfo.DirectoryName ?? string.Empty, newName);

            // Handle conflicts
            newPath = await ResolveConflictAsync(newPath, action.ConflictResolution);
            if (newPath == null)
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
            {
                await Task.Run(() => File.Delete(newPath));
            }

            // Retry logic
            await RetryAsync(async () =>
            {
                await Task.Run(() => File.Move(fileInfo.FullName, newPath));
            });

            return new ActionResult
            {
                Success = true,
                ResultPath = newPath,
                Type = ActionResultType.Success
            };
        }
        catch (Exception ex)
        {
            return new ActionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Type = ActionResultType.Error
            };
        }
    }

    private async Task<ActionResult> ExecuteChangeExtensionAsync(ChangeExtensionAction action, FileInfo fileInfo)
    {
        try
        {
            var newExtension = action.NewExtension.TrimStart('.');
            var nameWithoutExt = Path.GetFileNameWithoutExtension(fileInfo.Name);
            var newPath = Path.Combine(fileInfo.DirectoryName ?? string.Empty, $"{nameWithoutExt}.{newExtension}");

            // Handle conflicts
            newPath = await ResolveConflictAsync(newPath, action.ConflictResolution);
            if (newPath == null)
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
            {
                await Task.Run(() => File.Delete(newPath));
            }

            // Retry logic
            await RetryAsync(async () =>
            {
                await Task.Run(() => File.Move(fileInfo.FullName, newPath));
            });

            return new ActionResult
            {
                Success = true,
                ResultPath = newPath,
                Type = ActionResultType.Success
            };
        }
        catch (Exception ex)
        {
            return new ActionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Type = ActionResultType.Error
            };
        }
    }

    private async Task<ActionResult> ExecuteDeleteAsync(DeleteFileAction action, FileInfo fileInfo)
    {
        try
        {
            await RetryAsync(async () =>
            {
                await Task.Run(() =>
                {
                    if (action.UseRecycleBin)
                    {
                        // Use Visual Basic FileSystem for Recycle Bin support
                        FileSystem.DeleteFile(fileInfo.FullName, 
                            UIOption.OnlyErrorDialogs, 
                            RecycleOption.SendToRecycleBin);
                    }
                    else
                    {
                        File.Delete(fileInfo.FullName);
                    }
                });
            });

            // Remove empty parent folders if requested
            if (action.RemoveEmptyFolders)
            {
                await RemoveEmptyFoldersAsync(fileInfo.Directory);
            }

            return new ActionResult
            {
                Success = true,
                Type = ActionResultType.Success
            };
        }
        catch (Exception ex)
        {
            return new ActionResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Type = ActionResultType.Error
            };
        }
    }

    private async Task<ActionResult> ExecuteExtractArchiveAsync(ExtractArchiveAction action, FileInfo fileInfo, int? counter)
    {
        try
        {
            var destPath = _variableEngine.Resolve(action.DestinationPath, fileInfo, counter);
            
            // If no destination specified, extract to same folder
            if (string.IsNullOrWhiteSpace(destPath))
            {
                destPath = fileInfo.DirectoryName ?? string.Empty;
            }

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
            {
                await Task.Run(() => File.Delete(fileInfo.FullName));
            }

            return new ActionResult
            {
                Success = true,
                ResultPath = destPath,
                Type = ActionResultType.Success
            };
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
            var command = _variableEngine.Resolve(action.Command, fileInfo, counter);
            var workingDir = string.IsNullOrWhiteSpace(action.WorkingDirectory) 
                ? fileInfo.DirectoryName 
                : _variableEngine.Resolve(action.WorkingDirectory, fileInfo, counter);

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
                var completed = await Task.Run(() => 
                    process.WaitForExit(action.TimeoutSeconds * 1000));

                if (!completed)
                {
                    process.Kill();
                    return new ActionResult
                    {
                        Success = false,
                        ErrorMessage = $"Command timed out after {action.TimeoutSeconds} seconds",
                        Type = ActionResultType.Error
                    };
                }

                var output = await process.StandardOutput.ReadToEndAsync();
                var error = await process.StandardError.ReadToEndAsync();

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

            return new ActionResult
            {
                Success = true,
                ResultPath = fileInfo.FullName,
                Type = ActionResultType.Success
            };
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

    private async Task<string?> ResolveConflictAsync(string destinationPath, ConflictResolution strategy)
    {
        if (!File.Exists(destinationPath))
            return destinationPath;

        return strategy switch
        {
            ConflictResolution.Skip => null,
            ConflictResolution.Overwrite => destinationPath,
            ConflictResolution.RenameNew => await GenerateUniquePathAsync(destinationPath),
            ConflictResolution.RenameOld => await RenameExistingFileAsync(destinationPath),
            ConflictResolution.Prompt => destinationPath, // TODO: Implement user prompt
            _ => null
        };
    }

    private async Task<string> GenerateUniquePathAsync(string originalPath)
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

        return await Task.FromResult(newPath);
    }

    private async Task<string> RenameExistingFileAsync(string destinationPath)
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

    private async Task RemoveEmptyFoldersAsync(DirectoryInfo? directory)
    {
        if (directory == null || !directory.Exists)
            return;

        try
        {
            // Don't delete if contains files or hidden/system files
            if (directory.GetFiles().Length > 0)
                return;

            var subdirs = directory.GetDirectories();
            if (subdirs.Length > 0)
                return;

            await Task.Run(() => directory.Delete());

            // Recursively remove parent if empty
            await RemoveEmptyFoldersAsync(directory.Parent);
        }
        catch
        {
            // Silently fail - not critical
        }
    }

    private async Task RetryAsync(Func<Task> action)
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

        // If all retries failed, throw the last exception
        if (lastException != null)
            throw lastException;
    }

    public async Task<List<ActionPreview>> PreviewActionsAsync(List<FileAction> actions, FileInfo fileInfo, int? counter = null)
    {
        var previews = new List<ActionPreview>();
        var currentPath = fileInfo.FullName;
        var currentName = fileInfo.Name;

        foreach (var action in actions.OrderBy(a => a.Order))
        {
            var preview = action switch
            {
                MoveFileAction move => PreviewMove(move, currentPath, currentName, counter, fileInfo),
                CopyFileAction copy => PreviewCopy(copy, currentPath, currentName, counter, fileInfo),
                RenameFileAction rename => PreviewRename(rename, currentPath, currentName, counter, fileInfo),
                ChangeExtensionAction changeExt => PreviewChangeExtension(changeExt, currentPath, currentName),
                DeleteFileAction delete => PreviewDelete(delete, currentPath),
                ExtractArchiveAction extract => PreviewExtractArchive(extract, currentPath, counter, fileInfo),
                RunCommandAction runCmd => PreviewRunCommand(runCmd, currentPath, counter, fileInfo),
                _ => new ActionPreview { ActionType = "Unknown", Description = "Unknown action type" }
            };

            previews.Add(preview);

            // Update current path for next action if this action would change it
            if (!string.IsNullOrEmpty(preview.ResultPath))
            {
                currentPath = preview.ResultPath;
                currentName = Path.GetFileName(currentPath);
            }
        }

        return await Task.FromResult(previews);
    }

    private ActionPreview PreviewMove(MoveFileAction action, string currentPath, string currentName, int? counter, FileInfo fileInfo)
    {
        var destPath = _variableEngine.Resolve(action.DestinationPath, fileInfo, counter);
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

    private ActionPreview PreviewCopy(CopyFileAction action, string currentPath, string currentName, int? counter, FileInfo fileInfo)
    {
        var destPath = _variableEngine.Resolve(action.DestinationPath, fileInfo, counter);
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

    private ActionPreview PreviewRename(RenameFileAction action, string currentPath, string currentName, int? counter, FileInfo fileInfo)
    {
        var newName = _variableEngine.Resolve(action.NamePattern, fileInfo, counter);
        if (!Path.HasExtension(newName))
        {
            newName += fileInfo.Extension;
        }

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

    private ActionPreview PreviewChangeExtension(ChangeExtensionAction action, string currentPath, string currentName)
    {
        var newExt = action.NewExtension.StartsWith(".") ? action.NewExtension : "." + action.NewExtension;
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

    private ActionPreview PreviewDelete(DeleteFileAction action, string currentPath)
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
        var destPath = _variableEngine.Resolve(action.DestinationPath, fileInfo, counter);
        if (string.IsNullOrWhiteSpace(destPath))
        {
            destPath = Path.GetDirectoryName(currentPath) ?? string.Empty;
        }

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
        var command = _variableEngine.Resolve(action.Command, fileInfo, counter);
        return new ActionPreview
        {
            ActionType = "Run Command",
            Description = $"Execute: {command}",
            ResultPath = currentPath,
            HasConflict = false
        };
    }
}
