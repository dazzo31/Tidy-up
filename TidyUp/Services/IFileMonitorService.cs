using System.IO;
using TidyUp.Models.Domain;

namespace TidyUp.Services;

/// <summary>
/// Service for monitoring file system changes.
/// </summary>
public interface IFileMonitorService
{
    /// <summary>
    /// Event raised when a file is detected that needs processing.
    /// </summary>
    event EventHandler<FileDetectedEventArgs>? FileDetected;

    /// <summary>
    /// Starts monitoring based on the provided rules.
    /// </summary>
    Task StartAsync(List<Rule> rules, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops all file monitoring.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Updates the monitored rules without stopping the service.
    /// </summary>
    Task UpdateRulesAsync(List<Rule> rules);

    /// <summary>
    /// Manually scans all monitored folders for existing files.
    /// </summary>
    Task ScanAllFoldersAsync();

    /// <summary>
    /// Indicates whether monitoring is currently paused.
    /// </summary>
    bool IsPaused { get; }

    /// <summary>
    /// Number of file detection events currently buffered while paused.
    /// </summary>
    int PausedBufferedEventCount { get; }

    /// <summary>
    /// Pauses file monitoring. Watcher events will be safely buffered in memory instead of dispatched.
    /// </summary>
    void Pause();

    /// <summary>
    /// Resumes file monitoring and safely flushes all buffered events.
    /// </summary>
    Task ResumeAsync();
}

/// <summary>
/// Event args for file detection.
/// </summary>
public class FileDetectedEventArgs : EventArgs
{
    public FileInfo FileInfo { get; set; } = null!;
    public string SourceFolder { get; set; } = string.Empty;
}
