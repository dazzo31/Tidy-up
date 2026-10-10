using TidyUp.Models.Domain;

namespace TidyUp.Services.Simulation;

/// <summary>
/// Service interface for generating dry-run execution plans from rules and target directories
/// without mutating the filesystem or updating processed file state.
/// </summary>
public interface IExecutionPlanGenerator
{
    /// <summary>
    /// Generates an execution plan for a rule by simulating across all its configured monitored folders.
    /// </summary>
    Task<ExecutionPlan> GeneratePlanAsync(Rule rule, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates an execution plan for a rule targeting a specific directory.
    /// </summary>
    Task<ExecutionPlan> GeneratePlanForFolderAsync(
        Rule rule,
        string folderPath,
        bool includeSubfolders,
        IEnumerable<string>? exclusionPatterns = null,
        CancellationToken cancellationToken = default);
}

