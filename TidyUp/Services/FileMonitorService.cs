using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using TidyUp.Models.Domain;
using TidyUp.Services.FileSystem;
using TidyUp.Services.Monitoring;
using TidyUp.Services.Processing;
using TidyUp.Services.Watcher;

namespace TidyUp.Services;

/// <summary>
/// Implementation of file monitoring service with debouncing, 64KB hardened watchers, and overflow reconciliation.
/// </summary>
public class FileMonitorService : IFileMonitorService, IWatcherReconciler, IDisposable
{
    private readonly ConcurrentDictionary<string, HardenedFileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTime> _fileChangeTimestamps = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, bool> _reconnectingFolders = new(StringComparer.OrdinalIgnoreCase);
    private readonly CompositeDisposable _subscriptions = new();
    private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(500);
    private readonly IFileLockDetector _fileLockDetector;
    private readonly IRetryQueueManager? _retryQueueManager;
    private readonly IWatcherHealthMonitor? _watcherHealthMonitor;
    private readonly ConcurrentQueue<(string FilePath, string SourceFolder)> _pausedEventQueue = new();
    private volatile bool _isPaused;
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);
    private readonly ConcurrentDictionary<string, byte> _pausedFolders = new(StringComparer.OrdinalIgnoreCase);
    private CancellationToken _applicationStoppingToken;
    private CancellationTokenSource? _runCts;
    private List<Rule> _rules = new();
    private bool _isRunning;
    private bool _disposed;

    public bool IsRunning => _isRunning;
    public bool IsRunTokenCancelled => _runCts?.IsCancellationRequested ?? true;

    public event EventHandler<FileDetectedEventArgs>? FileDetected;
    public event EventHandler<string>? ReconciliationTriggered;

    public bool IsPaused => _isPaused;
    public int PausedBufferedEventCount => _pausedEventQueue.Count;

    public FileMonitorService(
        IFileLockDetector? fileLockDetector = null,
        IRetryQueueManager? retryQueueManager = null,
        IWatcherHealthMonitor? watcherHealthMonitor = null)
    {
        _fileLockDetector = fileLockDetector ?? new FileLockDetector();
        _retryQueueManager = retryQueueManager;
        _watcherHealthMonitor = watcherHealthMonitor;

        if (_retryQueueManager is not null)
        {
            _retryQueueManager.FileReady += OnRetryFileReady;
        }

        if (_watcherHealthMonitor is not null)
        {
            _watcherHealthMonitor.FolderHealthChanged += OnFolderHealthChanged;
        }
    }

    public void PauseFolder(string folderPath)
    {
        var canonical = CanonicalizePath(folderPath);
        if (!string.IsNullOrEmpty(canonical))
        {
            _pausedFolders[canonical] = 0;
            _watcherHealthMonitor?.SetFolderPaused(canonical, true);
        }
    }

    public void ResumeFolder(string folderPath)
    {
        var canonical = CanonicalizePath(folderPath);
        if (!string.IsNullOrEmpty(canonical))
        {
            _pausedFolders.TryRemove(canonical, out _);
            _watcherHealthMonitor?.SetFolderPaused(canonical, false);
        }
    }

    public bool IsFolderPaused(string folderPath)
    {
        var canonical = CanonicalizePath(folderPath);
        return !string.IsNullOrEmpty(canonical) && _pausedFolders.ContainsKey(canonical);
    }

    public void Pause()
    {
        _isPaused = true;
    }

    public async Task ResumeAsync()
    {
        _isPaused = false;
        var processedInBatch = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (_pausedEventQueue.TryDequeue(out var item))
        {
            if (_runCts?.IsCancellationRequested == true)
                break;

            if (!processedInBatch.Add(item.FilePath))
                continue;

            _fileChangeTimestamps.TryRemove(item.FilePath, out _);

            await OnFileDetectedAsync(item.FilePath, item.SourceFolder);
        }
    }

    private void OnRetryFileReady(object? sender, FileDetectedEventArgs e)
    {
        if (_isPaused)
        {
            _pausedEventQueue.Enqueue((e.FileInfo.FullName, e.SourceFolder));
            return;
        }

        FileDetected?.Invoke(this, e);
    }

    public async Task StartAsync(List<Rule> rules, CancellationToken cancellationToken = default)
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            if (_isRunning)
                await StopInternalAsync();

            _applicationStoppingToken = cancellationToken;
            _runCts?.Dispose();
            _runCts = CancellationTokenSource.CreateLinkedTokenSource(_applicationStoppingToken);

            _rules = rules.Where(r => r.IsEnabled).ToList();
            _isRunning = true;

            _retryQueueManager?.Start();
            _watcherHealthMonitor?.Start();

            await ConfigureWatchersAsync();
            await ScanAllFoldersAsync();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public async Task StopAsync()
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            await StopInternalAsync();
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    private Task StopInternalAsync()
    {
        _isRunning = false;
        _isPaused = false;
        _pausedEventQueue.Clear();
        _runCts?.Cancel();

        _retryQueueManager?.Stop();
        _watcherHealthMonitor?.Stop();

        // Dispose all Rx subscriptions
        _subscriptions.Clear();

        // Dispose all watchers
        foreach (var watcher in _watchers.Values)
        {
            watcher.Stop();
            watcher.Dispose();
        }

        _watchers.Clear();
        _fileChangeTimestamps.Clear();

        _runCts?.Dispose();
        _runCts = null;

        return Task.CompletedTask;
    }

    public async Task UpdateRulesAsync(List<Rule> rules)
    {
        await _lifecycleLock.WaitAsync();
        try
        {
            var wasRunning = _isRunning;
            if (wasRunning)
            {
                await StopInternalAsync();
            }

            _rules = rules.Where(r => r.IsEnabled).ToList();

            if (wasRunning)
            {
                // Instantiate fresh uncancelled run-loop token linked to the application lifetime token
                _runCts?.Dispose();
                _runCts = CancellationTokenSource.CreateLinkedTokenSource(_applicationStoppingToken);
                _isRunning = true;

                _retryQueueManager?.Start();
                _watcherHealthMonitor?.Start();

                await ConfigureWatchersAsync();
                await ScanAllFoldersAsync();
            }
        }
        finally
        {
            _lifecycleLock.Release();
        }
    }

    public static string CanonicalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (!string.IsNullOrEmpty(root) && string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase))
                return fullPath;

            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.TrimEnd('\\', '/');
        }
    }

    public bool TryGetWatcherIncludeSubdirectories(string folderPath, out bool includeSubdirectories)
    {
        var canonical = CanonicalizePath(folderPath);
        if (_watchers.TryGetValue(canonical, out var watcher))
        {
            includeSubdirectories = watcher.IncludeSubdirectories;
            return true;
        }

        includeSubdirectories = false;
        return false;
    }

    public static bool IsInDirectoryScope(string filePath, string folderPath, bool includeSubfolders)
    {
        var canonicalFolder = CanonicalizePath(folderPath);
        var fileDir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrEmpty(fileDir))
            return false;

        var canonicalFileDir = CanonicalizePath(fileDir);

        if (!includeSubfolders)
        {
            return string.Equals(canonicalFileDir, canonicalFolder, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(canonicalFileDir, canonicalFolder, StringComparison.OrdinalIgnoreCase))
            return true;

        var folderPrefix = canonicalFolder.EndsWith(Path.DirectorySeparatorChar.ToString())
            ? canonicalFolder
            : canonicalFolder + Path.DirectorySeparatorChar;

        return canonicalFileDir.StartsWith(folderPrefix, StringComparison.OrdinalIgnoreCase);
    }

    public bool IsFileEligibleForFolder(string filePath, MonitoredFolder folder)
    {
        if (!IsInDirectoryScope(filePath, folder.Path, folder.IncludeSubfolders))
            return false;

        foreach (var rawPattern in folder.ExclusionPatterns)
        {
            if (string.IsNullOrWhiteSpace(rawPattern))
                continue;

            var pattern = rawPattern.Trim();

            if (filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return false;

            if (pattern.StartsWith("*."))
            {
                var ext = pattern.Substring(1);
                if (filePath.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            else if (pattern.StartsWith("*") || pattern.EndsWith("*"))
            {
                var trimmed = pattern.Trim('*');
                if (!string.IsNullOrEmpty(trimmed) && filePath.Contains(trimmed, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        return true;
    }

    private bool IsFileEligibleForAnyFolder(string filePath, IEnumerable<MonitoredFolder> folders)
    {
        if (_fileLockDetector.IsTemporaryOrIncompleteFile(filePath))
            return false;

        return folders.Any(f => IsFileEligibleForFolder(filePath, f));
    }

    private async Task ConfigureWatchersAsync()
    {
        var folderGroups = _rules
            .SelectMany(r => r.MonitoredFolders)
            .Where(f => !string.IsNullOrWhiteSpace(f.Path))
            .GroupBy(f => CanonicalizePath(f.Path), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in folderGroups)
        {
            await SetupFolderMonitoringAsync(group.Key, group.ToList());
        }
    }

    private Task SetupFolderMonitoringAsync(string canonicalPath, List<MonitoredFolder> folders)
    {
        if (!Directory.Exists(canonicalPath))
        {
            _watcherHealthMonitor?.RegisterFolder(canonicalPath);
            return Task.CompletedTask;
        }

        if (IsNetworkPath(canonicalPath))
        {
            _watcherHealthMonitor?.RegisterFolder(canonicalPath);
            return Task.CompletedTask;
        }

        // Multilateral watcher scope merging (TASK-REM-26):
        // IncludeSubdirectories is true if ANY rule watching this path requires subfolders
        bool includeSubfolders = folders.Any(f => f.IncludeSubfolders);

        var watcher = new HardenedFileSystemWatcher(canonicalPath, new HardenedWatcherOptions
        {
            IncludeSubdirectories = includeSubfolders,
            DebounceDelay = _debounceDelay
        });

        watcher.FileDetected += async (s, path) =>
        {
            if (IsFolderPaused(canonicalPath))
                return;

            if (IsFileEligibleForAnyFolder(path, folders))
            {
                await OnFileDetectedAsync(path, canonicalPath);
            }
        };

        watcher.BufferOverflow += (s, e) =>
        {
            _watcherHealthMonitor?.RecordDegradation(canonicalPath, e.GetException()?.Message ?? "Internal buffer overflow");
        };

        watcher.ReconciliationRequested += async (s, path) =>
        {
            await ReconcileFolderAsync(canonicalPath, includeSubfolders, Array.Empty<string>());
        };

        watcher.Start();
        _watchers[canonicalPath] = watcher;
        _watcherHealthMonitor?.RegisterFolder(canonicalPath);

        return Task.CompletedTask;
    }

    public async Task ScanAllFoldersAsync()
    {
        var folderGroups = _rules
            .SelectMany(r => r.MonitoredFolders)
            .Where(f => !string.IsNullOrWhiteSpace(f.Path))
            .GroupBy(f => CanonicalizePath(f.Path), StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var group in folderGroups)
        {
            var canonicalPath = group.Key;
            if (!Directory.Exists(canonicalPath) || IsFolderPaused(canonicalPath))
                continue;

            bool includeSubfolders = group.Any(f => f.IncludeSubfolders);
            await ScanFolderGroupAsync(canonicalPath, includeSubfolders, group.ToList());
        }
    }

    private async Task ScanFolderGroupAsync(string canonicalPath, bool includeSubfolders, List<MonitoredFolder> folders)
    {
        try
        {
            var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(canonicalPath, "*.*", searchOption);

            foreach (var filePath in files)
            {
                if (_runCts?.IsCancellationRequested == true)
                    return;

                if (IsFileEligibleForAnyFolder(filePath, folders))
                {
                    await OnFileDetectedAsync(filePath, canonicalPath);
                }
            }
        }
        catch (UnauthorizedAccessException) { }
        catch (DirectoryNotFoundException) { }
    }

    public async Task ReconcileFolderAsync(
        string folderPath,
        bool includeSubfolders,
        IReadOnlyList<string> exclusionPatterns,
        CancellationToken cancellationToken = default)
    {
        ReconciliationTriggered?.Invoke(this, folderPath);
        var canonicalPath = CanonicalizePath(folderPath);

        var matchingFolders = _rules
            .SelectMany(r => r.MonitoredFolders)
            .Where(f => string.Equals(CanonicalizePath(f.Path), canonicalPath, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (matchingFolders.Count > 0)
        {
            bool effectiveSubfolders = includeSubfolders || matchingFolders.Any(f => f.IncludeSubfolders);
            await ScanFolderGroupAsync(canonicalPath, effectiveSubfolders, matchingFolders);
        }
        else
        {
            var folderDef = new MonitoredFolder
            {
                Path = folderPath,
                IncludeSubfolders = includeSubfolders,
                ExclusionPatterns = new ObservableCollection<string>(exclusionPatterns)
            };
            await ScanFolderGroupAsync(canonicalPath, includeSubfolders, new List<MonitoredFolder> { folderDef });
        }

        _watcherHealthMonitor?.ClearDegradation(folderPath);
    }

    private async void OnFolderHealthChanged(object? sender, FolderHealthReport e)
    {
        var canonicalReportPath = CanonicalizePath(e.Path);
        if (e.Status == WatcherHealthStatus.Healthy && !_watchers.ContainsKey(canonicalReportPath) && _isRunning)
        {
            if (!_reconnectingFolders.TryAdd(canonicalReportPath, true))
                return;

            try
            {
                var matchingFolders = _rules
                    .SelectMany(r => r.MonitoredFolders)
                    .Where(f => string.Equals(CanonicalizePath(f.Path), canonicalReportPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();

                if (matchingFolders.Count > 0)
                {
                    await SetupFolderMonitoringAsync(canonicalReportPath, matchingFolders);
                    bool includeSubfolders = matchingFolders.Any(f => f.IncludeSubfolders);
                    await ScanFolderGroupAsync(canonicalReportPath, includeSubfolders, matchingFolders);
                }
            }
            finally
            {
                _reconnectingFolders.TryRemove(canonicalReportPath, out _);
            }
        }
    }

    private bool ShouldProcessFile(string filePath, List<string> exclusionPatterns)
    {
        // Skip if file doesn't exist
        if (!File.Exists(filePath))
            return false;

        // Check exclusion patterns
        foreach (var pattern in exclusionPatterns)
        {
            if (filePath.Contains(pattern, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Skip temporary or incomplete download files (.crdownload, .part, .tmp, ~$*, etc.)
        if (_fileLockDetector.IsTemporaryOrIncompleteFile(filePath))
            return false;

        return true;
    }

    private async Task OnFileDetectedAsync(string filePath, string sourceFolder)
    {
        try
        {
            if (_isPaused)
            {
                _pausedEventQueue.Enqueue((filePath, sourceFolder));
                return;
            }

            if (!File.Exists(filePath))
                return;

            if (_fileLockDetector.IsTemporaryOrIncompleteFile(filePath))
                return;

            var fileInfo = new FileInfo(filePath);
            
            // Check if file is still changing (debounce check)
            if (_fileChangeTimestamps.TryGetValue(filePath, out var lastChange))
            {
                if (DateTime.Now - lastChange < _debounceDelay)
                    return; // Still changing, skip
            }

            _fileChangeTimestamps[filePath] = DateTime.Now;

            // Wait a brief moment to ensure file is settled
            await Task.Delay(100);

            // Pre-flight check: is file available exclusively (FileShare.None)?
            if (!_fileLockDetector.IsFileReady(filePath))
            {
                // File is held open by another process (in-progress write/download);
                // enqueue to non-blocking retry queue rather than dropping or blocking.
                _retryQueueManager?.Enqueue(filePath, sourceFolder);
                return;
            }

            // Raise event
            FileDetected?.Invoke(this, new FileDetectedEventArgs
            {
                FileInfo = fileInfo,
                SourceFolder = sourceFolder
            });
        }
        catch (Exception)
        {
            // Log and continue
        }
    }

    private bool IsNetworkPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return false;

        // Check for UNC path
        if (path.StartsWith("\\\\"))
            return true;

        // Check if mapped network drive
        try
        {
            var root = Path.GetPathRoot(path);
            if (root != null)
            {
                var driveInfo = new DriveInfo(root);
                return driveInfo.DriveType == DriveType.Network;
            }
        }
        catch
        {
            // If we can't determine, assume local
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _isRunning = false;
        _isPaused = false;
        _pausedEventQueue.Clear();
        _runCts?.Cancel();
        _subscriptions.Dispose();

        if (_retryQueueManager is not null)
        {
            _retryQueueManager.FileReady -= OnRetryFileReady;
        }

        if (_watcherHealthMonitor is not null)
        {
            _watcherHealthMonitor.FolderHealthChanged -= OnFolderHealthChanged;
        }

        foreach (var watcher in _watchers.Values)
        {
            watcher.Stop();
            watcher.Dispose();
        }

        _watchers.Clear();
        _runCts?.Dispose();
        _runCts = null;
        _lifecycleLock.Dispose();
        GC.SuppressFinalize(this);
    }
}
