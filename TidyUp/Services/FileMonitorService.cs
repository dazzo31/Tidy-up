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
    private List<Rule> _rules = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning;
    private bool _disposed;

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
            if (_cancellationTokenSource?.IsCancellationRequested == true)
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
        if (_isRunning)
            await StopAsync();

        _rules = rules.Where(r => r.IsEnabled).ToList();
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isRunning = true;

        _retryQueueManager?.Start();
        _watcherHealthMonitor?.Start();

        // Set up watchers for all monitored folders
        var monitoredFolders = _rules
            .SelectMany(r => r.MonitoredFolders)
            .DistinctBy(f => f.Path)
            .ToList();

        foreach (var folder in monitoredFolders)
        {
            await SetupFolderMonitoringAsync(folder);
        }

        // Initial scan of all folders
        await ScanAllFoldersAsync();
    }

    public Task StopAsync()
    {
        _isRunning = false;
        _isPaused = false;
        _pausedEventQueue.Clear();
        _cancellationTokenSource?.Cancel();

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

        return Task.CompletedTask;
    }

    public async Task UpdateRulesAsync(List<Rule> rules)
    {
        var wasRunning = _isRunning;
        
        if (wasRunning)
        {
            await StopAsync();
        }

        _rules = rules.Where(r => r.IsEnabled).ToList();

        if (wasRunning && _cancellationTokenSource is not null)
        {
            await StartAsync(_rules, _cancellationTokenSource.Token);
        }
    }

    public async Task ScanAllFoldersAsync()
    {
        var monitoredFolders = _rules
            .SelectMany(r => r.MonitoredFolders)
            .DistinctBy(f => f.Path)
            .ToList();

        foreach (var folder in monitoredFolders)
        {
            if (!Directory.Exists(folder.Path))
                continue;

            await ScanFolderAsync(folder.Path, folder.IncludeSubfolders, folder.ExclusionPatterns.ToList());
        }
    }

    private Task SetupFolderMonitoringAsync(MonitoredFolder folder)
    {
        if (!Directory.Exists(folder.Path))
        {
            _watcherHealthMonitor?.RegisterFolder(folder.Path);
            return Task.CompletedTask;
        }

        // Check if it's a network drive
        if (IsNetworkPath(folder.Path))
        {
            // For network drives, use polling instead of FileSystemWatcher
            // TODO: Implement periodic polling
            _watcherHealthMonitor?.RegisterFolder(folder.Path);
            return Task.CompletedTask;
        }

        var watcher = new HardenedFileSystemWatcher(folder.Path, new HardenedWatcherOptions
        {
            IncludeSubdirectories = folder.IncludeSubfolders,
            DebounceDelay = _debounceDelay
        });

        watcher.FileDetected += async (s, path) =>
        {
            if (ShouldProcessFile(path, folder.ExclusionPatterns.ToList()))
            {
                await OnFileDetectedAsync(path, folder.Path);
            }
        };

        watcher.BufferOverflow += (s, e) =>
        {
            _watcherHealthMonitor?.RecordDegradation(folder.Path, e.GetException()?.Message ?? "Internal buffer overflow");
        };

        watcher.ReconciliationRequested += async (s, path) =>
        {
            await ReconcileFolderAsync(path, folder.IncludeSubfolders, folder.ExclusionPatterns.ToList());
        };

        watcher.Start();
        _watchers[folder.Path] = watcher;

        _watcherHealthMonitor?.RegisterFolder(folder.Path);

        return Task.CompletedTask;
    }

    public async Task ReconcileFolderAsync(
        string folderPath,
        bool includeSubfolders,
        IReadOnlyList<string> exclusionPatterns,
        CancellationToken cancellationToken = default)
    {
        ReconciliationTriggered?.Invoke(this, folderPath);
        await ScanFolderAsync(folderPath, includeSubfolders, exclusionPatterns.ToList());
        _watcherHealthMonitor?.ClearDegradation(folderPath);
    }

    private async void OnFolderHealthChanged(object? sender, FolderHealthReport e)
    {
        if (e.Status == WatcherHealthStatus.Healthy && !_watchers.ContainsKey(e.Path) && _isRunning)
        {
            if (!_reconnectingFolders.TryAdd(e.Path, true))
                return;

            try
            {
                var folder = _rules
                    .SelectMany(r => r.MonitoredFolders)
                    .FirstOrDefault(f => string.Equals(f.Path, e.Path, StringComparison.OrdinalIgnoreCase));

                if (folder != null)
                {
                    await SetupFolderMonitoringAsync(folder);
                    await ScanFolderAsync(folder.Path, folder.IncludeSubfolders, folder.ExclusionPatterns.ToList());
                }
            }
            finally
            {
                _reconnectingFolders.TryRemove(e.Path, out _);
            }
        }
    }

    private async Task ScanFolderAsync(string folderPath, bool includeSubfolders, List<string> exclusionPatterns)
    {
        try
        {
            var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(folderPath, "*.*", searchOption);

            foreach (var filePath in files)
            {
                if (_cancellationTokenSource?.IsCancellationRequested == true)
                    return;

                if (ShouldProcessFile(filePath, exclusionPatterns))
                {
                    await OnFileDetectedAsync(filePath, folderPath);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Log and continue � folder inaccessible
        }
        catch (DirectoryNotFoundException) { }
        {
            // Folder was removed between check and scan
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
        _cancellationTokenSource?.Cancel();
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
        _cancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}
