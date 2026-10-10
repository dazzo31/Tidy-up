namespace TidyUp.Models.Domain;

/// <summary>
/// Result of executing an action on a file.
/// </summary>
public class ActionResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ResultPath { get; set; }
    public string? TargetPath { get; set; }
    public ActionResultType Type { get; set; } = ActionResultType.Success;
}

public enum ActionResultType
{
    Success,
    Warning,
    Error,
    Skipped
}
