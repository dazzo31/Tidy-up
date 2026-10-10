namespace TidyUp.Services.State;

/// <summary>
/// Application-wide lifecycle states for TidyUp.
/// </summary>
public enum ApplicationLifecycleState
{
    /// <summary>
    /// Base idle state. Background monitoring is stopped, no operations running.
    /// </summary>
    Idle,

    /// <summary>
    /// User is configuring or editing a rule. Rule is in unvalidated state until simulated.
    /// </summary>
    ConfiguringRule,

    /// <summary>
    /// A dry-run simulation is running in the background.
    /// </summary>
    Simulating,

    /// <summary>
    /// A simulation has completed and the impact preview is ready for inspection.
    /// </summary>
    PreviewReady,

    /// <summary>
    /// A batch of file operations is actively executing on disk.
    /// </summary>
    ExecutingBatch,

    /// <summary>
    /// Real-time file system monitoring is actively watching folders.
    /// </summary>
    MonitoringRunning,

    /// <summary>
    /// Real-time monitoring is paused; no new jobs are accepted while in-flight operations finish.
    /// </summary>
    MonitoringPaused
}

/// <summary>
/// Event arguments for application lifecycle state transitions.
/// </summary>
public class StateChangedEventArgs : EventArgs
{
    public ApplicationLifecycleState PreviousState { get; }
    public ApplicationLifecycleState NewState { get; }
    public string? Reason { get; }

    public StateChangedEventArgs(ApplicationLifecycleState previousState, ApplicationLifecycleState newState, string? reason = null)
    {
        PreviousState = previousState;
        NewState = newState;
        Reason = reason;
    }
}

