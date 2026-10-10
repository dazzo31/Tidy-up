using System.Collections.Concurrent;
using System.IO;
using System.Reactive.Disposables;
using System.Reactive.Linq;

namespace TidyUp.Services.Watcher;

/// <summary>
/// Configuration options for HardenedFileSystemWatcher.
/// </summary>
public class HardenedWatcherOptions
{
    /// <summary>
    /// Size of internal buffer in bytes. Maximum reliable size on Windows is 64KB (65536 bytes).
    /// </summary>
    public int InternalBufferSize { get; set; } = 65536;

    /// <summary>
    /// Window duration for per-path event throttling.
    /// </summary>
    public TimeSpan DebounceDelay { get; set; } = TimeSpan.FromMilliseconds(500);

    /// <summary>
    /// Whether subdirectories should also be watched.
    /// </summary>
    public bool IncludeSubdirectories { get; set; }

    /// <summary>
    /// File system change notification filters.
    /// </summary>
    public NotifyFilters NotifyFilter { get; set; } =
        NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.CreationTime;
}

/// <summary>
/// Hardened wrapper around FileSystemWatcher providing a 64KB internal buffer,
/// per-path sliding-window debouncing, error/overflow interception, and automatic reconciliation triggers.
/// </summary>
public class HardenedFileSystemWatcher : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly CompositeDisposable _subscriptions = new();
    private readonly ConcurrentDictionary<string, DateTime> _recentPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly TimeSpan _debounceDelay;
    private bool _disposed;

    /// <summary>
    /// Directory currently monitored by this watcher.
    /// </summary>
    public string WatchedPath { get; }

    /// <summary>
    /// Internal buffer size allocated to this watcher in bytes.
    /// </summary>
    public int InternalBufferSize => _watcher.InternalBufferSize;

    /// <summary>
    /// Whether events are currently actively being monitored.
    /// </summary>
    public bool IsRunning => _watcher.EnableRaisingEvents;

    /// <summary>
    /// Event raised when a file change event is debounced and ready for downstream processing.
    /// </summary>
    public event EventHandler<string>? FileDetected;

    /// <summary>
    /// Event raised when an internal buffer overflow or watcher error occurs.
    /// </summary>
    public event EventHandler<ErrorEventArgs>? BufferOverflow;

    /// <summary>
    /// Event raised when directory reconciliation is requested due to overflow or recovery.
    /// </summary>
    public event EventHandler<string>? ReconciliationRequested;

    public HardenedFileSystemWatcher(string path, HardenedWatcherOptions? options = null)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path cannot be empty.", nameof(path));

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Directory not found: {path}");

        WatchedPath = path;
        var opt = options ?? new HardenedWatcherOptions();
        _debounceDelay = opt.DebounceDelay;

        _watcher = new FileSystemWatcher(path)
        {
            InternalBufferSize = opt.InternalBufferSize,
            IncludeSubdirectories = opt.IncludeSubdirectories,
            NotifyFilter = opt.NotifyFilter,
            EnableRaisingEvents = false
        };

        SetupRxPipelines();
    }

    private void SetupRxPipelines()
    {
        // 1. Subscribe to Error event for Buffer Overflows
        _watcher.Error += OnWatcherError;

        // 2. Created and Changed events with per-path throttling
        var createdChanged = Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                h => _watcher.Created += h,
                h => _watcher.Created -= h)
            .Merge(Observable.FromEventPattern<FileSystemEventHandler, FileSystemEventArgs>(
                h => _watcher.Changed += h,
                h => _watcher.Changed -= h))
            .Select(e => e.EventArgs.FullPath);

        // 3. Renamed events: clean old path tracking and throttle new path
        var renamed = Observable.FromEventPattern<RenamedEventHandler, RenamedEventArgs>(
                h => _watcher.Renamed += h,
                h => _watcher.Renamed -= h)
            .Do(e =>
            {
                if (!string.IsNullOrEmpty(e.EventArgs.OldFullPath))
                {
                    _recentPaths.TryRemove(e.EventArgs.OldFullPath, out _);
                }
            })
            .Select(e => e.EventArgs.FullPath);

        // 4. Per-path group debouncer
        var debouncedStream = createdChanged
            .Merge(renamed)
            .GroupBy(path => path, StringComparer.OrdinalIgnoreCase)
            .SelectMany(group => group.Throttle(_debounceDelay))
            .Subscribe(path =>
            {
                if (!_disposed)
                {
                    FileDetected?.Invoke(this, path);
                }
            });

        _subscriptions.Add(debouncedStream);
    }

    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        BufferOverflow?.Invoke(this, e);
        ReconciliationRequested?.Invoke(this, WatchedPath);
    }

    /// <summary>
    /// Starts raising events from the file system.
    /// </summary>
    public void Start()
    {
        if (!_disposed)
        {
            _watcher.EnableRaisingEvents = true;
        }
    }

    /// <summary>
    /// Pauses raising events from the file system.
    /// </summary>
    public void Stop()
    {
        if (!_disposed)
        {
            _watcher.EnableRaisingEvents = false;
        }
    }

    /// <summary>
    /// Simulates a buffer overflow for automated testing and recovery verification.
    /// </summary>
    public void SimulateBufferOverflow(Exception? ex = null)
    {
        var errorArgs = new ErrorEventArgs(ex ?? new InternalBufferOverflowException("Simulated 64KB buffer overflow"));
        OnWatcherError(this, errorArgs);
    }

    /// <summary>
    /// Explicitly triggers a directory reconciliation scan request.
    /// </summary>
    public void TriggerReconciliation()
    {
        ReconciliationRequested?.Invoke(this, WatchedPath);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _watcher.EnableRaisingEvents = false;
        _watcher.Error -= OnWatcherError;
        _subscriptions.Dispose();
        _watcher.Dispose();
        _recentPaths.Clear();
        GC.SuppressFinalize(this);
    }
}

