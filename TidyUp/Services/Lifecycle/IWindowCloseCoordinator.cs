using System;
using System.Threading;
using System.Threading.Tasks;
using TidyUp.Models;
using TidyUp.Services.State;

namespace TidyUp.Services.Lifecycle;

/// <summary>
/// Desired action when window close or application exit is requested.
/// </summary>
public enum WindowCloseAction
{
    /// <summary>Close the window and permit process termination.</summary>
    CloseAndExit,

    /// <summary>Cancel window close and minimize window to system tray.</summary>
    MinimizeToTray,

    /// <summary>Cancel closing entirely and keep the window open and visible.</summary>
    Cancel
}

/// <summary>
/// Single coordinator for window closing, minimize-to-tray, and application shutdown decisions.
/// Eliminates conflicting event-handler registration order between MainWindow and App.
/// </summary>
public interface IWindowCloseCoordinator
{
    /// <summary>
    /// Evaluates the appropriate close action based on active batches, unsaved edits, and tray settings.
    /// </summary>
    WindowCloseAction EvaluateCloseAction(
        bool isRuleDirty,
        Func<bool>? confirmDiscardEdits,
        Func<string, string, bool>? confirmWaitBatch,
        bool isExplicitExit = false);

    /// <summary>
    /// Asynchronously coordinates application exit (e.g. from tray menu), waiting for in-flight batches.
    /// </summary>
    Task<bool> RequestApplicationExitAsync(
        Func<bool>? confirmDiscardEdits,
        Func<string, string, bool>? confirmForceExit,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of IWindowCloseCoordinator.
/// </summary>
public class WindowCloseCoordinator : IWindowCloseCoordinator
{
    private readonly AppSettings _settings;
    private readonly IApplicationStateManager? _stateManager;

    public WindowCloseCoordinator(
        AppSettings settings,
        IApplicationStateManager? stateManager = null)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _stateManager = stateManager;
    }

    public WindowCloseAction EvaluateCloseAction(
        bool isRuleDirty,
        Func<bool>? confirmDiscardEdits,
        Func<string, string, bool>? confirmWaitBatch,
        bool isExplicitExit = false)
    {
        // 1. Check active batch operation
        if (_stateManager != null && 
            (_stateManager.CurrentState == ApplicationLifecycleState.ExecutingBatch || _stateManager.ActiveOperationCount > 0))
        {
            bool shouldWait = confirmWaitBatch != null
                ? confirmWaitBatch("A file batch operation is currently in progress. Wait for operations to finish?", "Operation in Progress")
                : true;

            if (shouldWait)
            {
                return WindowCloseAction.Cancel;
            }
        }

        // 2. Check unsaved rule edits
        if (isRuleDirty)
        {
            bool discard = confirmDiscardEdits != null
                ? confirmDiscardEdits()
                : true;

            if (!discard)
            {
                return WindowCloseAction.Cancel;
            }
        }

        // 3. Explicit exit, or tray disabled, or minimize-to-tray disabled -> exit
        if (isExplicitExit || !_settings.SystemTrayEnabled || !_settings.MinimizeToTray)
        {
            return WindowCloseAction.CloseAndExit;
        }

        // 4. Otherwise, minimize to tray
        return WindowCloseAction.MinimizeToTray;
    }

    public async Task<bool> RequestApplicationExitAsync(
        Func<bool>? confirmDiscardEdits,
        Func<string, string, bool>? confirmForceExit,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        // 1. Check active batch operations and wait up to timeout
        if (_stateManager != null && 
            (_stateManager.CurrentState == ApplicationLifecycleState.ExecutingBatch || _stateManager.ActiveOperationCount > 0))
        {
            var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(5);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(effectiveTimeout);

            try
            {
                while (_stateManager.CurrentState == ApplicationLifecycleState.ExecutingBatch || _stateManager.ActiveOperationCount > 0)
                {
                    if (cts.IsCancellationRequested)
                    {
                        bool force = confirmForceExit != null
                            ? confirmForceExit("File operations are still in progress. Force exit now?", "Exit TidyUp")
                            : true;

                        if (!force)
                        {
                            return false;
                        }
                        break;
                    }

                    await Task.Delay(100, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                bool force = confirmForceExit != null
                    ? confirmForceExit("File operations timed out. Force exit now?", "Exit TidyUp")
                    : true;

                if (!force)
                {
                    return false;
                }
            }
        }

        // 2. Check unsaved edits
        if (confirmDiscardEdits != null && !confirmDiscardEdits())
        {
            return false;
        }

        return true;
    }
}
