using TidyUp.Data.Entities;
using TidyUp.Models.Domain;

namespace TidyUp.Services.Diagnostics;

/// <summary>
/// Service interface for detecting and explaining divergences between simulation preview
/// and actual execution results (e.g. locked files, disappeared files, permissions, or modifications).
/// </summary>
public interface IExecutionDiscrepancyAnalyzer
{
    /// <summary>
    /// Analyzes discrepancy between an execution plan and the resulting journal entries.
    /// </summary>
    ExecutionDiscrepancyReport Analyze(
        ExecutionPlan plan,
        IEnumerable<OperationJournalEntry> journalEntries,
        Guid? batchId = null);

    /// <summary>
    /// Analyzes discrepancy between a collection of planned actions and journal entries.
    /// </summary>
    ExecutionDiscrepancyReport Analyze(
        IEnumerable<PlannedFileAction> plannedActions,
        IEnumerable<OperationJournalEntry> journalEntries,
        Guid? batchId = null);

    /// <summary>
    /// Analyzes discrepancy between planned actions and action execution results.
    /// </summary>
    ExecutionDiscrepancyReport AnalyzeFromResults(
        IEnumerable<PlannedFileAction> plannedActions,
        IEnumerable<(PlannedFileAction Action, ActionResult Result)> executionResults,
        Guid? batchId = null);
}

