namespace TidyUp.Models.Enums;

/// <summary>
/// Represents the different views available in the main window.
/// </summary>
public enum NavigationView
{
    /// <summary>
    /// Operational dashboard overview (default).
    /// </summary>
    Dashboard,

    /// <summary>
    /// Rule editor view.
    /// </summary>
    RuleEditor,
    
    /// <summary>
    /// Settings view.
    /// </summary>
    Settings,
    
    /// <summary>
    /// Log viewer view.
    /// </summary>
    Logs,
    
    /// <summary>
    /// Help and About view.
    /// </summary>
    Help,

    /// <summary>
    /// History and rollback journal view.
    /// </summary>
    History
}
