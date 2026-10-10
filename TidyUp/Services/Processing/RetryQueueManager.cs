using System.Collections.Concurrent;
using System.IO;
using TidyUp.Services.FileSystem;

namespace TidyUp.Services.Processing;

/// <summary>
/// Thread-safe non-blocking retry queue manager with configurable backoff.
/// </summary>
public class RetryQueueManager : IRetryQueueManager
{
    private class RetryItem
    {
        public required string FilePath { get; init; }
        public required string SourceFolder { get; init; }
        public int AttemptCount { get; set; }
        public int MaxRetries { get; init; }
        public DateTime NextAttemptAt { get; set; }
    }

    private readonly IFileLockDetector _fileLockDetector;
    private readonly IReadOnlyList<TimeSpan> _backoffDelays;
    private readonly TimeSpan _pollInterval;
    private readonly ConcurrentDictionary<string, RetryItem> _queue = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _syncLock = new();

    private CancellationTokenSource? _cts;
    private Task? _backgroundWorkerTask;
    private bool _isDisposed;

    public event EventHandler<FileDetectedEventArgs>? FileReady;
    public event EventHandler<RetryFailedEventArgs>? FileRetryFailed;

    public int QueueCount => _queue.Count;

    public RetryQueueManager(
        IFileLockDetector fileLockDetector,
        IReadOnlyList<TimeSpan>? backoffDelays = null,
        TimeSpan? pollInterval = null)
    {
        _fileLockDetector = fileLockDetector ?? throw new ArgumentNullException(nameof(fileLockDetector));
        _backoffDelays = backoffDelays ?? new[]
        {
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5)
        };
        _pollInterval = pollInterval ?? TimeSpan.FromMilliseconds(250);
    }

    public void Enqueue(string filePath, string sourceFolder, int maxRetries = 3)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        var normalizedKey = Path.GetFullPath(filePath);
        var initialDelay = _backoffDelays.Count > 0 ? _backoffDelays[0] : TimeSpan.FromSeconds(1);

        _queue.AddOrUpdate(
            normalizedKey,
            _ => new RetryItem
            {
                FilePath = filePath,
                SourceFolder = sourceFolder,
                AttemptCount = 0,
                MaxRetries = maxRetries,
                NextAttemptAt = DateTime.UtcNow + initialDelay
            },
            (_, existing) =>
            {
                // If already queued, preserve attempt count but ensure due time is set
                return existing;
            });
    }

    public async Task ProcessQueueAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var dueItems = _queue.Values
            .Where(item => now >= item.NextAttemptAt)
            .ToList();

        foreach (var item in dueItems)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            if (!File.Exists(item.FilePath))
            {
                _queue.TryRemove(item.FilePath, out _);
                continue;
            }

            if (_fileLockDetector.IsTemporaryOrIncompleteFile(item.FilePath))
            {
                AdvanceOrExpire(item, "File is still an incomplete download or temporary file");
                continue;
            }

            if (_fileLockDetector.IsFileReady(item.FilePath))
            {
                if (_queue.TryRemove(item.FilePath, out _))
                {
                    FileReady?.Invoke(this, new FileDetectedEventArgs
                    {
                        FileInfo = new FileInfo(item.FilePath),
                        SourceFolder = item.SourceFolder
                    });
                }
            }
            else
            {
                AdvanceOrExpire(item, "File is locked by an external process");
            }
        }

        await Task.CompletedTask;
    }

    private void AdvanceOrExpire(RetryItem item, string failureReason)
    {
        item.AttemptCount++;

        if (item.AttemptCount < item.MaxRetries)
        {
            var delayIndex = Math.Min(item.AttemptCount, _backoffDelays.Count - 1);
            var delay = _backoffDelays[delayIndex];
            item.NextAttemptAt = DateTime.UtcNow + delay;
        }
        else
        {
            if (_queue.TryRemove(item.FilePath, out _))
            {
                FileRetryFailed?.Invoke(this, new RetryFailedEventArgs
                {
                    FilePath = item.FilePath,
                    SourceFolder = item.SourceFolder,
                    AttemptCount = item.AttemptCount,
                    Reason = $"{failureReason} after {item.AttemptCount} retries."
                });
            }
        }
    }

    public void Start()
    {
        lock (_syncLock)
        {
            if (_backgroundWorkerTask is not null || _isDisposed)
                return;

            _cts = new CancellationTokenSource();
            _backgroundWorkerTask = Task.Run(async () =>
            {
                using var timer = new PeriodicTimer(_pollInterval);
                try
                {
                    while (await timer.WaitForNextTickAsync(_cts.Token))
                    {
                        await ProcessQueueAsync(_cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Clean cancellation
                }
            });
        }
    }

    public void Stop()
    {
        lock (_syncLock)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _backgroundWorkerTask = null;
        }
    }

    public void Dispose()
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        Stop();
        _queue.Clear();
    }
}

