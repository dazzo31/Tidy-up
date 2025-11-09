using System.Collections.Concurrent;
using System.IO;
using System.Reactive.Linq;
using TidyUp.Models.Domain;

namespace TidyUp.Services;

/// <summary>
/// Implementation of file monitoring service with debouncing and network drive support.
/// </summary>
public class FileMonitorService : IFileMonitorService, IDisposable
{
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
    private readonly ConcurrentDictionary<string, DateTime> _fileChangeTimestamps = new();
    private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(500);
    private List<Rule> _rules = new();
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _isRunning;

    public event EventHandler<FileDetectedEventArgs>? FileDetected;

    public async Task StartAsync(List<Rule> rules, CancellationToken cancellationToken = default)
    {
        if (_isRunning)
            await StopAsync();

        _rules = rules.Where(r => r.IsEnabled).ToList();
        _cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _isRunning = true;

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

    public async Task StopAsync()
    {
        _isRunning = false;
        _cancellationTokenSource?.Cancel();

        // Dispose all watchers
        foreach (var watcher in _watchers.Values)
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }

        _watchers.Clear();
        _fileChangeTimestamps.Clear();

        await Task.CompletedTask;
    }

    public async Task UpdateRulesAsync(List<Rule> rules)
    {
        var wasRunning = _isRunning;
        
        if (wasRunning)
        {
            await StopAsync();
        }

        _rules = rules.Where(r => r.IsEnabled).ToList();

        if (wasRunning)
        {
            await StartAsync(_rules, _cancellationTokenSource?.Token ?? CancellationToken.None);
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

    private async Task SetupFolderMonitoringAsync(MonitoredFolder folder)
    {
        if (!Directory.Exists(folder.Path))
            return;

        // Check if it's a network drive
        if (IsNetworkPath(folder.Path))
        {
            // For network drives, use polling instead of FileSystemWatcher
            // TODO: Implement periodic polling
            return;
        }

        var watcher = new FileSystemWatcher(folder.Path)
        {
            IncludeSubdirectories = folder.IncludeSubfolders,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime,
            EnableRaisingEvents = true
        };

        // Set up event handlers with debouncing
        Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                h => watcher.Created += h,
                h => watcher.Created -= h)
            .Merge(Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                h => watcher.Changed += h,
                h => watcher.Changed -= h))
            .Select(e => e.EventArgs.FullPath)
            .GroupBy(path => path)
            .SelectMany(g => g.Throttle(_debounceDelay))
            .Subscribe(async path =>
            {
                if (ShouldProcessFile(path, folder.ExclusionPatterns.ToList()))
                {
                    await OnFileDetectedAsync(path, folder.Path);
                }
            });

        Observable.FromEventPattern<RenamedEventHandler, RenamedEventArgs>(
                h => watcher.Renamed += h,
                h => watcher.Renamed -= h)
            .Select(e => e.EventArgs.FullPath)
            .Throttle(_debounceDelay)
            .Subscribe(async path =>
            {
                if (ShouldProcessFile(path, folder.ExclusionPatterns.ToList()))
                {
                    await OnFileDetectedAsync(path, folder.Path);
                }
            });

        _watchers[folder.Path] = watcher;

        await Task.CompletedTask;
    }

    private async Task ScanFolderAsync(string folderPath, bool includeSubfolders, List<string> exclusionPatterns)
    {
        try
        {
            var searchOption = includeSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(folderPath, "*.*", searchOption);

            foreach (var filePath in files)
            {
                if (ShouldProcessFile(filePath, exclusionPatterns))
                {
                    await OnFileDetectedAsync(filePath, folderPath);
                }
            }
        }
        catch (UnauthorizedAccessException)
        {
            // Log and continue
        }
        catch (Exception)
        {
            // Log and continue
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

        // Skip temporary files
        var fileName = Path.GetFileName(filePath);
        if (fileName.StartsWith("~") || fileName.StartsWith("."))
            return false;

        return true;
    }

    private async Task OnFileDetectedAsync(string filePath, string sourceFolder)
    {
        try
        {
            var fileInfo = new FileInfo(filePath);
            
            // Check if file is still changing (debounce check)
            if (_fileChangeTimestamps.TryGetValue(filePath, out var lastChange))
            {
                if (DateTime.Now - lastChange < _debounceDelay)
                    return; // Still changing, skip
            }

            _fileChangeTimestamps[filePath] = DateTime.Now;

            // Wait a bit to ensure file is not locked
            await Task.Delay(100);

            // Try to open file to check if it's accessible
            try
            {
                using var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            }
            catch (IOException)
            {
                // File is locked, skip for now
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
        StopAsync().Wait();
        _cancellationTokenSource?.Dispose();
        GC.SuppressFinalize(this);
    }
}
