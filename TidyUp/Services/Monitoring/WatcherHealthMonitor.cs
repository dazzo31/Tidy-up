using System.Collections.Concurrent;
using System.IO;

namespace TidyUp.Services.Monitoring;

/// <summary>
/// Monitors health, accessibility, and degraded states of watched directories.
/// Performs periodic heartbeats, manages reconnection backoff schedules,
/// and detects unplugged drives, severed network shares, and buffer degradation.
/// </summary>
public class WatcherHealthMonitor : IWatcherHealthMonitor
{
    private class FolderState
    {
        public string Path { get; }
        public bool IsNetworkShare { get; }
        public bool IsPaused { get; set; }
        public bool IsDegraded { get; set; }
        public string? DegradationReason { get; set; }
        public WatcherHealthStatus Status { get; set; } = WatcherHealthStatus.Healthy;
        public string? StatusDetail { get; set; }
        public DateTime LastCheckedUtc { get; set; } = DateTime.UtcNow;
        public DateTime? LastSuccessfulCheckUtc { get; set; }
        public int ConsecutiveFailures { get; set; }
        public TimeSpan CurrentBackoffDelay { get; set; } = TimeSpan.Zero;
        public DateTime? NextRetryTimeUtc { get; set; }

        public FolderState(string path, bool isNetworkShare, bool isPaused)
        {
            Path = path;
            IsNetworkShare = isNetworkShare;
            IsPaused = isPaused;
            if (isPaused)
            {
                Status = WatcherHealthStatus.Paused;
                StatusDetail = "Monitoring paused by user";
            }
        }

        public FolderHealthReport ToReport()
        {
            return new FolderHealthReport
            {
                Path = Path,
                Status = Status,
                StatusDetail = StatusDetail,
                LastCheckedUtc = LastCheckedUtc,
                LastSuccessfulCheckUtc = LastSuccessfulCheckUtc,
                ConsecutiveFailures = ConsecutiveFailures,
                IsNetworkShare = IsNetworkShare,
                NextRetryDelay = CurrentBackoffDelay
            };
        }
    }

    private readonly ConcurrentDictionary<string, FolderState> _folders = new(StringComparer.OrdinalIgnoreCase);
    private readonly Func<string, bool> _directoryExistsCheck;
    private readonly Action<string> _directoryAccessibilityProbe;
    private Timer? _heartbeatTimer;
    private bool _isRunning;
    private bool _disposed;

    public event EventHandler<FolderHealthReport>? FolderHealthChanged;

    public TimeSpan HeartbeatInterval { get; set; } = TimeSpan.FromSeconds(30);

    public bool IsRunning => _isRunning;

    public WatcherHealthMonitor(
        Func<string, bool>? directoryExistsCheck = null,
        Action<string>? directoryAccessibilityProbe = null)
    {
        _directoryExistsCheck = directoryExistsCheck ?? Directory.Exists;
        _directoryAccessibilityProbe = directoryAccessibilityProbe ?? DefaultAccessibilityProbe;
    }

    private static void DefaultAccessibilityProbe(string path)
    {
        // Lightly probe directory enumeration to detect broken UNC shares or access revocation
        using var enumerator = Directory.EnumerateFileSystemEntries(path).GetEnumerator();
        try
        {
            enumerator.MoveNext();
        }
        catch (InvalidOperationException)
        {
            // Empty folder - enumeration started cleanly
        }
    }

    public void Start()
    {
        if (_disposed || _isRunning)
            return;

        _isRunning = true;
        _heartbeatTimer = new Timer(
            OnHeartbeatTick,
            null,
            HeartbeatInterval,
            HeartbeatInterval);
    }

    public void Stop()
    {
        _isRunning = false;
        _heartbeatTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _heartbeatTimer?.Dispose();
        _heartbeatTimer = null;
    }

    private async void OnHeartbeatTick(object? state)
    {
        if (!_isRunning || _disposed)
            return;

        try
        {
            await CheckAllHealthAsync();
        }
        catch
        {
            // Suppress uncaught background timer exceptions
        }
    }

    public void RegisterFolder(string folderPath, bool isPaused = false)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        var isNetwork = IsNetworkPath(folderPath);
        var state = new FolderState(folderPath, isNetwork, isPaused);

        // Initial check without waiting for timer
        EvaluateFolderAccessibility(state, forceCheck: true);

        _folders[folderPath] = state;
        FolderHealthChanged?.Invoke(this, state.ToReport());
    }

    public void UnregisterFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        _folders.TryRemove(folderPath, out _);
    }

    public void SetFolderPaused(string folderPath, bool isPaused)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !_folders.TryGetValue(folderPath, out var state))
            return;

        state.IsPaused = isPaused;

        if (isPaused)
        {
            state.Status = WatcherHealthStatus.Paused;
            state.StatusDetail = "Monitoring paused by user";
            FolderHealthChanged?.Invoke(this, state.ToReport());
        }
        else
        {
            // Resumed: re-evaluate immediate health
            EvaluateFolderAccessibility(state, forceCheck: true);
            FolderHealthChanged?.Invoke(this, state.ToReport());
        }
    }

    public void RecordDegradation(string folderPath, string reason)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
            return;

        var state = _folders.GetOrAdd(folderPath, path => new FolderState(path, IsNetworkPath(path), isPaused: false));
        state.IsDegraded = true;
        state.DegradationReason = reason;

        if (state.Status == WatcherHealthStatus.Healthy || state.Status == WatcherHealthStatus.Degraded)
        {
            state.Status = WatcherHealthStatus.Degraded;
            state.StatusDetail = $"Watcher degraded: {reason}";
            FolderHealthChanged?.Invoke(this, state.ToReport());
        }
    }

    public void ClearDegradation(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !_folders.TryGetValue(folderPath, out var state))
            return;

        state.IsDegraded = false;
        state.DegradationReason = null;

        if (state.Status == WatcherHealthStatus.Degraded)
        {
            state.Status = WatcherHealthStatus.Healthy;
            state.StatusDetail = "Watcher healthy and operating normally";
            FolderHealthChanged?.Invoke(this, state.ToReport());
        }
    }

    public Task CheckAllHealthAsync(CancellationToken cancellationToken = default)
    {
        foreach (var state in _folders.Values)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var previousStatus = state.Status;
            var previousDetail = state.StatusDetail;

            EvaluateFolderAccessibility(state, forceCheck: false);

            if (state.Status != previousStatus || state.StatusDetail != previousDetail)
            {
                FolderHealthChanged?.Invoke(this, state.ToReport());
            }
        }

        return Task.CompletedTask;
    }

    public Task<FolderHealthReport> CheckFolderHealthAsync(string folderPath, CancellationToken cancellationToken = default)
    {
        if (!_folders.TryGetValue(folderPath, out var state))
        {
            var isNet = IsNetworkPath(folderPath);
            var tempState = new FolderState(folderPath, isNet, isPaused: false);
            EvaluateFolderAccessibility(tempState, forceCheck: true);
            return Task.FromResult(tempState.ToReport());
        }

        var previousStatus = state.Status;
        var previousDetail = state.StatusDetail;

        EvaluateFolderAccessibility(state, forceCheck: true);

        if (state.Status != previousStatus || state.StatusDetail != previousDetail)
        {
            FolderHealthChanged?.Invoke(this, state.ToReport());
        }

        return Task.FromResult(state.ToReport());
    }

    public FolderHealthReport? GetFolderHealth(string folderPath)
    {
        return _folders.TryGetValue(folderPath, out var state) ? state.ToReport() : null;
    }

    public IReadOnlyDictionary<string, FolderHealthReport> GetAllFolderHealth()
    {
        return _folders.ToDictionary(k => k.Key, v => v.Value.ToReport(), StringComparer.OrdinalIgnoreCase);
    }

    private void EvaluateFolderAccessibility(FolderState state, bool forceCheck)
    {
        state.LastCheckedUtc = DateTime.UtcNow;

        if (state.IsPaused)
        {
            state.Status = WatcherHealthStatus.Paused;
            state.StatusDetail = "Monitoring paused by user";
            return;
        }

        // If backoff delay has not elapsed and not forcing check, retain current disconnected status
        if (!forceCheck && state.NextRetryTimeUtc.HasValue && state.NextRetryTimeUtc.Value > DateTime.UtcNow)
        {
            return;
        }

        try
        {
            if (!_directoryExistsCheck(state.Path))
            {
                HandleFailure(state, WatcherHealthStatus.Disconnected,
                    state.IsNetworkShare
                        ? "Network share is unreachable or disconnected"
                        : "Directory does not exist or drive was removed");
                return;
            }

            // Probe directory accessibility to catch disconnected shares or permission locks
            _directoryAccessibilityProbe(state.Path);

            // Successfully reached and enumerated
            state.ConsecutiveFailures = 0;
            state.CurrentBackoffDelay = TimeSpan.Zero;
            state.NextRetryTimeUtc = null;
            state.LastSuccessfulCheckUtc = DateTime.UtcNow;

            if (state.IsDegraded)
            {
                state.Status = WatcherHealthStatus.Degraded;
                state.StatusDetail = $"Watcher degraded: {state.DegradationReason ?? "Buffer overflow detected"}";
            }
            else
            {
                state.Status = WatcherHealthStatus.Healthy;
                state.StatusDetail = "Directory is accessible and operational";
            }
        }
        catch (UnauthorizedAccessException ex)
        {
            HandleFailure(state, WatcherHealthStatus.Error, $"Permission denied: {ex.Message}");
        }
        catch (IOException ex)
        {
            HandleFailure(state, WatcherHealthStatus.Disconnected, $"I/O or network severed: {ex.Message}");
        }
        catch (Exception ex)
        {
            HandleFailure(state, WatcherHealthStatus.Error, $"Unexpected monitor error: {ex.Message}");
        }
    }

    private static void HandleFailure(FolderState state, WatcherHealthStatus status, string detail)
    {
        state.ConsecutiveFailures++;
        var backoff = CalculateBackoffDelay(state.ConsecutiveFailures);
        state.CurrentBackoffDelay = backoff;
        state.NextRetryTimeUtc = DateTime.UtcNow.Add(backoff);
        state.Status = status;
        state.StatusDetail = detail;
    }

    public static TimeSpan CalculateBackoffDelay(int consecutiveFailures)
    {
        return consecutiveFailures switch
        {
            <= 1 => TimeSpan.FromSeconds(5),
            2 => TimeSpan.FromSeconds(10),
            3 => TimeSpan.FromSeconds(20),
            4 => TimeSpan.FromSeconds(30),
            _ => TimeSpan.FromSeconds(60)
        };
    }

    public static bool IsNetworkPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return false;

        if (path.StartsWith(@"\\") || path.StartsWith("//"))
            return true;

        try
        {
            var root = Path.GetPathRoot(path);
            if (!string.IsNullOrEmpty(root))
            {
                var driveInfo = new DriveInfo(root);
                return driveInfo.DriveType == DriveType.Network;
            }
        }
        catch
        {
            // Ignore failure to query drive
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Stop();
        _folders.Clear();
        GC.SuppressFinalize(this);
    }
}
