namespace TidyUp.Services.State;

/// <summary>
/// Service interface for enforcing application-wide lifecycle states and workflow rules.
/// </summary>
public interface IApplicationStateManager
{
    /// <summary>
    /// Current application lifecycle state.
    /// </summary>
    ApplicationLifecycleState CurrentState { get; }

    /// <summary>
    /// Raised whenever the application transitions between lifecycle states.
    /// </summary>
    event EventHandler<StateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Indicates whether the currently active/edited rule configuration has been verified by simulation.
    /// When a rule is modified, this becomes false until simulated.
    /// </summary>
    bool IsRuleValidated { get; }

    /// <summary>
    /// Number of file operations currently in-flight.
    /// </summary>
    int ActiveOperationCount { get; }

    /// <summary>
    /// Determines whether a transition from CurrentState to nextState is permitted.
    /// </summary>
    bool CanTransitionTo(ApplicationLifecycleState nextState);

    /// <summary>
    /// Attempts to transition to the specified state according to the state transition matrix.
    /// Returns true if successful; false if rejected.
    /// </summary>
    bool TryTransitionTo(ApplicationLifecycleState nextState, string? reason = null);

    /// <summary>
    /// Transitions to the specified state or throws InvalidOperationException if transition is illegal.
    /// </summary>
    void TransitionTo(ApplicationLifecycleState nextState, string? reason = null);

    /// <summary>
    /// Notifies that a rule is being edited or modified.
    /// Transitions state to ConfiguringRule and resets IsRuleValidated to false.
    /// </summary>
    void NotifyRuleModified(string? ruleName = null);

    /// <summary>
    /// Notifies that rule simulation has finished. Sets IsRuleValidated to true and transitions to PreviewReady.
    /// </summary>
    void NotifyRuleSimulated();

    /// <summary>
    /// Notifies that rule configuration was saved. Transitions state to Idle without starting monitoring.
    /// </summary>
    void NotifyRuleSaved();

    /// <summary>
    /// Notifies that a file operation has started execution. Increments active operation count.
    /// </summary>
    void NotifyOperationStarted();

    /// <summary>
    /// Notifies that a file operation has finished execution. Decrements active operation count.
    /// </summary>
    void NotifyOperationCompleted();
}

