using System.IO;

namespace TidyUp.Services.Processing;

/// <summary>
/// Event arguments for files that failed all retry attempts.
/// </summary>
public class RetryFailedEventArgs : EventArgs
{
    public string FilePath { get; init; } = string.Empty;
    public string SourceFolder { get; init; } = string.Empty;
    public int AttemptCount { get; init; }
    public string Reason { get; init; } = string.Empty;
}

/// <summary>
/// Manages a non-blocking retry queue with exponential backoff for files that are
/// temporarily locked or incomplete when first detected.
/// </summary>
public interface IRetryQueueManager : IDisposable
{
    /// <summary>
    /// Event raised when an enqueued file becomes available for exclusive access.
    /// </summary>
    event EventHandler<FileDetectedEventArgs>? FileReady;

    /// <summary>
    /// Event raised when an enqueued file exhausts its retry attempts.
    /// </summary>
    event EventHandler<RetryFailedEventArgs>? FileRetryFailed;

    /// <summary>
    /// The number of items currently pending in the retry queue.
    /// </summary>
    int QueueCount { get; }

    /// <summary>
    /// Enqueues a file for deferred retry.
    /// </summary>
    void Enqueue(string filePath, string sourceFolder, int maxRetries = 3);

    /// <summary>
    /// Evaluates all currently due items in the queue once.
    /// </summary>
    Task ProcessQueueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts background polling of the retry queue.
    /// </summary>
    void Start();

    /// <summary>
    /// Stops background polling of the retry queue.
    /// </summary>
    void Stop();
}

