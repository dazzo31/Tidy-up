namespace TidyUp.Services.Monitoring;

/// <summary>
/// Application-owned monitoring coordinator that loads enabled rules at startup,
/// activates FileMonitorService, and processes detected file events through the safe execution pipeline.
/// </summary>
public interface IMonitoringCoordinator : IDisposable
{
    /// <summary>
    /// Indicates whether background monitoring is currently active.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Starts background file monitoring with all currently enabled rules.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops background file monitoring and cancels active watchers.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Synchronizes the active monitoring scope with the current state of rules in the repository.
    /// </summary>
    Task RefreshRulesAsync();

    /// <summary>
    /// Processes a single detected file event through rule matching, simulation, and safe batch execution.
    /// </summary>
    Task ProcessFileDetectedAsync(string filePath, string sourceFolder);
}
