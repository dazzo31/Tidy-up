namespace TidyUp.Services.State;

/// <summary>
/// Thread-safe implementation of IApplicationStateManager.
/// Enforces application-wide lifecycle states and valid transition rules across the UI.
/// </summary>
public class ApplicationStateManager : IApplicationStateManager
{
    private readonly object _syncRoot = new();
    private ApplicationLifecycleState _currentState = ApplicationLifecycleState.Idle;
    private ApplicationLifecycleState _preExecutionState = ApplicationLifecycleState.Idle;
    private int _activeOperationCount;
    private bool _isRuleValidated;
    private Guid? _activeBatchLeaseId;

    public ApplicationLifecycleState CurrentState
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentState;
            }
        }
    }

    public event EventHandler<StateChangedEventArgs>? StateChanged;

    public bool IsRuleValidated
    {
        get
        {
            lock (_syncRoot)
            {
                return _isRuleValidated;
            }
        }
    }

    public int ActiveOperationCount
    {
        get
        {
            lock (_syncRoot)
            {
                return _activeOperationCount;
            }
        }
    }

    public bool CanTransitionTo(ApplicationLifecycleState nextState)
    {
        lock (_syncRoot)
        {
            return IsValidTransition(_currentState, nextState);
        }
    }

    public bool TryTransitionTo(ApplicationLifecycleState nextState, string? reason = null)
    {
        StateChangedEventArgs? args = null;

        lock (_syncRoot)
        {
            if (_currentState == nextState)
                return true;

            if (!IsValidTransition(_currentState, nextState))
                return false;

            var previousState = _currentState;
            _currentState = nextState;
            args = new StateChangedEventArgs(previousState, nextState, reason);
        }

        StateChanged?.Invoke(this, args);
        return true;
    }

    public void TransitionTo(ApplicationLifecycleState nextState, string? reason = null)
    {
        if (!TryTransitionTo(nextState, reason))
        {
            throw new InvalidOperationException(
                $"Illegal state transition: Cannot transition from '{CurrentState}' to '{nextState}'.");
        }
    }

    public void NotifyRuleModified(string? ruleName = null)
    {
        lock (_syncRoot)
        {
            if (_currentState == ApplicationLifecycleState.ExecutingBatch)
            {
                throw new InvalidOperationException("Cannot modify rule configuration while operations are executing.");
            }

            _isRuleValidated = false;

            if (_currentState != ApplicationLifecycleState.ConfiguringRule)
            {
                TryTransitionTo(ApplicationLifecycleState.ConfiguringRule, $"Rule modified: {ruleName ?? "unnamed"}");
            }
        }
    }

    public void NotifyRuleSimulated()
    {
        lock (_syncRoot)
        {
            _isRuleValidated = true;
            TryTransitionTo(ApplicationLifecycleState.PreviewReady, "Simulation completed");
        }
    }

    public void NotifyRuleSaved()
    {
        lock (_syncRoot)
        {
            // Workflow Rule 1: Saving a rule transitions to Idle without starting monitoring
            TryTransitionTo(ApplicationLifecycleState.Idle, "Rule saved to storage");
        }
    }

    public void NotifyOperationStarted()
    {
        lock (_syncRoot)
        {
            _activeOperationCount++;

            if (_currentState != ApplicationLifecycleState.ExecutingBatch)
            {
                _preExecutionState = _currentState;
                TryTransitionTo(ApplicationLifecycleState.ExecutingBatch, "File operation batch started");
            }
        }
    }

    public void NotifyOperationCompleted()
    {
        lock (_syncRoot)
        {
            if (_activeOperationCount > 0)
            {
                _activeOperationCount--;
            }

            // Only transition back if no batch lease is currently holding ExecutingBatch
            if (!_activeBatchLeaseId.HasValue && _activeOperationCount == 0 && _currentState == ApplicationLifecycleState.ExecutingBatch)
            {
                // Return to pre-execution state or Idle
                var returnState = _preExecutionState switch
                {
                    ApplicationLifecycleState.MonitoringRunning => ApplicationLifecycleState.MonitoringRunning,
                    ApplicationLifecycleState.MonitoringPaused => ApplicationLifecycleState.MonitoringPaused,
                    ApplicationLifecycleState.PreviewReady => ApplicationLifecycleState.PreviewReady,
                    _ => ApplicationLifecycleState.Idle
                };

                TryTransitionTo(returnState, "All in-flight operations completed");
            }
        }
    }

    public bool TryAcquireBatchLease(Guid batchId, string? reason = null)
    {
        lock (_syncRoot)
        {
            if (_activeBatchLeaseId.HasValue && _activeBatchLeaseId.Value != batchId)
            {
                return false;
            }

            if (_currentState != ApplicationLifecycleState.ExecutingBatch)
            {
                if (!CanTransitionTo(ApplicationLifecycleState.ExecutingBatch))
                {
                    return false;
                }

                _preExecutionState = _currentState;
                TryTransitionTo(ApplicationLifecycleState.ExecutingBatch, reason ?? "Batch lease acquired");
            }

            _activeBatchLeaseId = batchId;
            return true;
        }
    }

    public void ReleaseBatchLease(Guid batchId, string? reason = null)
    {
        lock (_syncRoot)
        {
            if (_activeBatchLeaseId != batchId)
            {
                return;
            }

            _activeBatchLeaseId = null;

            if (_activeOperationCount == 0 && _currentState == ApplicationLifecycleState.ExecutingBatch)
            {
                var returnState = _preExecutionState switch
                {
                    ApplicationLifecycleState.MonitoringRunning => ApplicationLifecycleState.MonitoringRunning,
                    ApplicationLifecycleState.MonitoringPaused => ApplicationLifecycleState.MonitoringPaused,
                    ApplicationLifecycleState.PreviewReady => ApplicationLifecycleState.PreviewReady,
                    _ => ApplicationLifecycleState.Idle
                };

                TryTransitionTo(returnState, reason ?? "Batch lease released");
            }
        }
    }

    private static bool IsValidTransition(ApplicationLifecycleState from, ApplicationLifecycleState to)
    {
        if (from == to)
            return true;

        return from switch
        {
            ApplicationLifecycleState.Idle => to is
                ApplicationLifecycleState.ConfiguringRule or
                ApplicationLifecycleState.Simulating or
                ApplicationLifecycleState.ExecutingBatch or
                ApplicationLifecycleState.MonitoringRunning,

            ApplicationLifecycleState.ConfiguringRule => to is
                ApplicationLifecycleState.Simulating or
                ApplicationLifecycleState.Idle,

            ApplicationLifecycleState.Simulating => to is
                ApplicationLifecycleState.PreviewReady or
                ApplicationLifecycleState.ConfiguringRule or
                ApplicationLifecycleState.Idle,

            ApplicationLifecycleState.PreviewReady => to is
                ApplicationLifecycleState.ExecutingBatch or
                ApplicationLifecycleState.ConfiguringRule or
                ApplicationLifecycleState.Idle,

            ApplicationLifecycleState.ExecutingBatch => to is
                ApplicationLifecycleState.Idle or
                ApplicationLifecycleState.PreviewReady or
                ApplicationLifecycleState.MonitoringRunning or
                ApplicationLifecycleState.MonitoringPaused,

            ApplicationLifecycleState.MonitoringRunning => to is
                ApplicationLifecycleState.MonitoringPaused or
                ApplicationLifecycleState.ExecutingBatch or
                ApplicationLifecycleState.Idle,

            ApplicationLifecycleState.MonitoringPaused => to is
                ApplicationLifecycleState.MonitoringRunning or
                ApplicationLifecycleState.ExecutingBatch or
                ApplicationLifecycleState.ConfiguringRule or
                ApplicationLifecycleState.Idle,

            _ => false
        };
    }
}

