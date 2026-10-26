using System;
using System.Threading.Tasks;

namespace TidyUp.Services.Diagnostics;

/// <summary>
/// Data payload for an executed file action telemetry event.
/// </summary>
public class OperationTelemetryEvent
{
    public Guid ActionId { get; init; } = Guid.NewGuid();
    public Guid? BatchId { get; init; }
    public Guid? RuleId { get; init; }
    public string RuleName { get; init; } = string.Empty;
    public string? RuleRevisionId { get; init; }
    public string ActionType { get; init; } = string.Empty;
    public string SourcePath { get; init; } = string.Empty;
    public string? TargetPath { get; init; }
    public string Outcome { get; init; } = "Success"; // Success, Warning, Error
    public string? ErrorMessage { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}

/// <summary>
/// Service contract for publishing and streaming executed file operations across Dashboard, Logs, and diagnostics.
/// </summary>
public interface IOperationTelemetryService
{
    /// <summary>
    /// Event raised whenever an operation executes and is durably committed.
    /// </summary>
    event EventHandler<OperationTelemetryEvent>? OperationExecuted;

    /// <summary>
    /// Records an executed file action, persisting it to durable logs and notifying live subscribers.
    /// Deduplicates duplicate action event deliveries.
    /// </summary>
    Task RecordOperationAsync(OperationTelemetryEvent telemetryEvent);
}
