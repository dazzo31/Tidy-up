using System.IO;
using TidyUp.Models.Domain;

namespace TidyUp.Services;

/// <summary>
/// Service for executing file actions.
/// </summary>
public interface IActionExecutor
{
    /// <summary>
    /// Executes all actions for a rule on a specific file.
    /// </summary>
    /// <param name="actions">List of actions to execute in order.</param>
    /// <param name="fileInfo">The file to operate on.</param>
    /// <param name="counter">Optional counter for variable resolution.</param>
    /// <returns>List of action results.</returns>
    Task<List<ActionResult>> ExecuteActionsAsync(List<FileAction> actions, FileInfo fileInfo, int? counter = null);

    /// <summary>
    /// Executes a single action on a file.
    /// </summary>
    Task<ActionResult> ExecuteActionAsync(FileAction action, FileInfo fileInfo, int? counter = null);

    /// <summary>
    /// Previews what actions would do without executing them (dry run).
    /// </summary>
    /// <param name="actions">List of actions to preview.</param>
    /// <param name="fileInfo">The file to preview actions on.</param>
    /// <param name="counter">Optional counter for variable resolution.</param>
    /// <returns>List of action previews showing what would happen.</returns>
    Task<List<ActionPreview>> PreviewActionsAsync(List<FileAction> actions, FileInfo fileInfo, int? counter = null);
}
