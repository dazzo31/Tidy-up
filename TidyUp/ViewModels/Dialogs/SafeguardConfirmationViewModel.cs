using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;

namespace TidyUp.ViewModels.Dialogs;

/// <summary>
/// Type of risk detected on a planned file action.
/// </summary>
public enum FileRiskType
{
    PermanentDeletion,
    FileOverwrite,
    LargeBatchThreshold
}

/// <summary>
/// Presentation item representing an individual file facing a destructive or high-risk operation.
/// </summary>
public class FileRiskItemViewModel
{
    public string FilePath { get; set; } = string.Empty;
    public string? TargetPath { get; set; }
    public string RuleName { get; set; } = string.Empty;
    public FileRiskType RiskType { get; set; }
    public string Description { get; set; } = string.Empty;

    public string RiskBadgeText => RiskType switch
    {
        FileRiskType.PermanentDeletion => "PERMANENT DELETE",
        FileRiskType.FileOverwrite => "OVERWRITE",
        FileRiskType.LargeBatchThreshold => "LARGE BATCH",
        _ => "WARNING"
    };

    public string RiskBadgeBackground => RiskType switch
    {
        FileRiskType.PermanentDeletion => "#FFEBEE",
        FileRiskType.FileOverwrite => "#FFF3E0",
        FileRiskType.LargeBatchThreshold => "#EDE7F6",
        _ => "#FFF3E0"
    };

    public string RiskBadgeForeground => RiskType switch
    {
        FileRiskType.PermanentDeletion => "#C62828",
        FileRiskType.FileOverwrite => "#E65100",
        FileRiskType.LargeBatchThreshold => "#4A148C",
        _ => "#E65100"
    };
}

/// <summary>
/// ViewModel for the Destructive-Action Safeguards & Batch Confirmations dialog.
/// Evaluates actions for permanent deletion, overwriting, and volume thresholds.
/// </summary>
public partial class SafeguardConfirmationViewModel : ObservableObject
{
    /// <summary>
    /// Configurable threshold for triggering a high-volume warning (default: 50 files).
    /// </summary>
    [ObservableProperty]
    private int _batchThreshold = 50;

    [ObservableProperty]
    private bool _hasPermanentDeletionRisk;

    [ObservableProperty]
    private bool _hasOverwriteRisk;

    [ObservableProperty]
    private bool _hasLargeBatchRisk;

    [ObservableProperty]
    private int _permanentDeletionCount;

    [ObservableProperty]
    private int _overwriteCount;

    [ObservableProperty]
    private int _totalBatchCount;

    [ObservableProperty]
    private string _riskSummary = string.Empty;

    [ObservableProperty]
    private ObservableCollection<FileRiskItemViewModel> _filesAtRisk = new();

    /// <summary>
    /// True if the user confirmed the dialog; false if cancelled or dismissed.
    /// </summary>
    [ObservableProperty]
    private bool _isConfirmed;

    public Action? RequestClose { get; set; }

    public SafeguardConfirmationViewModel(int batchThreshold = 50)
    {
        BatchThreshold = batchThreshold;
    }

    /// <summary>
    /// Evaluates planned actions to determine whether any destructive or high-risk thresholds are exceeded.
    /// Returns true if safeguards are triggered and confirmation is required; false otherwise.
    /// </summary>
    public bool EvaluateRisks(IEnumerable<PlannedActionItemViewModel> items)
    {
        ArgumentNullException.ThrowIfNull(items);

        var itemList = items.Where(i => i.IsApproved && !i.IsExecuted).ToList();
        return EvaluatePlannedActions(itemList.Select(i => i.PlannedAction));
    }

    /// <summary>
    /// Evaluates planned actions to determine whether any destructive or high-risk thresholds are exceeded.
    /// </summary>
    public bool EvaluatePlannedActions(IEnumerable<PlannedFileAction> actions)
    {
        ArgumentNullException.ThrowIfNull(actions);

        var actionList = actions.ToList();
        TotalBatchCount = actionList.Count;
        FilesAtRisk.Clear();

        PermanentDeletionCount = 0;
        OverwriteCount = 0;
        HasPermanentDeletionRisk = false;
        HasOverwriteRisk = false;
        HasLargeBatchRisk = TotalBatchCount > BatchThreshold;

        foreach (var action in actionList)
        {
            // 1. Permanent deletion check (Delete without Recycle Bin)
            if (action.ActionType == ActionType.Delete &&
                action.FileAction is DeleteFileAction del &&
                !del.UseRecycleBin)
            {
                PermanentDeletionCount++;
                HasPermanentDeletionRisk = true;
                FilesAtRisk.Add(new FileRiskItemViewModel
                {
                    FilePath = action.SourcePath,
                    TargetPath = null,
                    RuleName = action.RuleName,
                    RiskType = FileRiskType.PermanentDeletion,
                    Description = "File will be permanently deleted without being placed in the Recycle Bin."
                });
            }

            // 2. Overwrite check (Move/Copy/Rename with Overwrite conflict resolution or collision)
            bool isOverwrite = false;
            if (action.ConflictResolution == ConflictResolution.Overwrite)
            {
                isOverwrite = true;
            }
            else if (action.HasConflict && action.ConflictDescription != null &&
                     action.ConflictDescription.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                isOverwrite = true;
            }

            if (isOverwrite)
            {
                OverwriteCount++;
                HasOverwriteRisk = true;
                FilesAtRisk.Add(new FileRiskItemViewModel
                {
                    FilePath = action.SourcePath,
                    TargetPath = action.TargetPath,
                    RuleName = action.RuleName,
                    RiskType = FileRiskType.FileOverwrite,
                    Description = $"Target file '{Path.GetFileName(action.TargetPath)}' will be overwritten if it exists."
                });
            }
        }

        // 3. Large batch risk items (if batch exceeds threshold, mention bulk impact)
        if (HasLargeBatchRisk && FilesAtRisk.Count == 0)
        {
            // Add sample items to show what will be affected
            foreach (var action in actionList.Take(10))
            {
                FilesAtRisk.Add(new FileRiskItemViewModel
                {
                    FilePath = action.SourcePath,
                    TargetPath = action.TargetPath,
                    RuleName = action.RuleName,
                    RiskType = FileRiskType.LargeBatchThreshold,
                    Description = $"Part of large batch operation ({TotalBatchCount} total files)."
                });
            }
        }

        BuildRiskSummary();

        // Safeguard triggers if ANY destructive operation is planned OR total batch count exceeds threshold
        return HasPermanentDeletionRisk || HasOverwriteRisk || HasLargeBatchRisk;
    }

    private void BuildRiskSummary()
    {
        var risks = new List<string>();

        if (HasPermanentDeletionRisk)
        {
            risks.Add($"• Permanent Deletion: {PermanentDeletionCount} file(s) will be permanently erased (cannot be recovered from Recycle Bin).");
        }

        if (HasOverwriteRisk)
        {
            risks.Add($"• File Overwrite: {OverwriteCount} destination file(s) will be overwritten.");
        }

        if (HasLargeBatchRisk)
        {
            risks.Add($"• High-Volume Operation: Batch contains {TotalBatchCount} files (safety threshold: {BatchThreshold}).");
        }

        RiskSummary = string.Join(Environment.NewLine, risks);
    }

    [RelayCommand]
    public void Confirm()
    {
        IsConfirmed = true;
        RequestClose?.Invoke();
    }

    [RelayCommand]
    public void Cancel()
    {
        IsConfirmed = false;
        RequestClose?.Invoke();
    }
}

