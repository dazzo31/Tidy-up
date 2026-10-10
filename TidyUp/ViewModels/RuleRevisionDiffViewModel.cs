using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyUp.Data.Entities;
using TidyUp.Services.Rules;

namespace TidyUp.ViewModels;

public partial class RuleRevisionDiffViewModel : ObservableObject
{
    private readonly IRuleRevisionManager _revisionManager;
    private readonly Guid _ruleId;

    [ObservableProperty]
    private ObservableCollection<RuleRevision> _revisions = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRestore))]
    private RuleRevision? _selectedRevision;

    [ObservableProperty]
    private RuleRevision? _comparisonRevision;

    [ObservableProperty]
    private RuleDiffResult? _currentDiff;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public bool CanRestore => SelectedRevision != null;

    public Func<string, string, bool> ConfirmRestoreHandler { get; set; } = (msg, title) =>
        System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question) == System.Windows.MessageBoxResult.Yes;

    public Action? OnRestoredCallback { get; set; }

    public RuleRevisionDiffViewModel(IRuleRevisionManager revisionManager, Guid ruleId)
    {
        _revisionManager = revisionManager ?? throw new ArgumentNullException(nameof(revisionManager));
        _ruleId = ruleId;
    }

    [RelayCommand]
    public async Task LoadRevisionsAsync()
    {
        var list = await _revisionManager.GetRevisionsForRuleAsync(_ruleId);
        Revisions = new ObservableCollection<RuleRevision>(list);

        if (Revisions.Count >= 2)
        {
            ComparisonRevision = Revisions[0]; // Latest
            SelectedRevision = Revisions[1];   // Prior
            ComputeDiff();
        }
        else if (Revisions.Count == 1)
        {
            SelectedRevision = Revisions[0];
            StatusMessage = "Rule only has 1 revision (no prior version to compare).";
        }
    }

    partial void OnSelectedRevisionChanged(RuleRevision? value)
    {
        ComputeDiff();
    }

    partial void OnComparisonRevisionChanged(RuleRevision? value)
    {
        ComputeDiff();
    }

    private void ComputeDiff()
    {
        if (SelectedRevision != null && ComparisonRevision != null && SelectedRevision.RevisionId != ComparisonRevision.RevisionId)
        {
            CurrentDiff = _revisionManager.DiffRevisions(SelectedRevision, ComparisonRevision);
            StatusMessage = CurrentDiff.HasChanges
                ? $"Differences between v{SelectedRevision.VersionNumber} and v{ComparisonRevision.VersionNumber} identified."
                : $"v{SelectedRevision.VersionNumber} and v{ComparisonRevision.VersionNumber} are identical.";
        }
        else
        {
            CurrentDiff = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRestore))]
    public async Task RestoreRevisionAsync()
    {
        if (SelectedRevision == null) return;

        var confirm = ConfirmRestoreHandler(
            $"Restore Rule to Version {SelectedRevision.VersionNumber} (created {SelectedRevision.CreatedAtUtc:g})?\n\nThis will update the active rule configuration.",
            "Confirm Restore Revision");

        if (!confirm) return;

        try
        {
            await _revisionManager.RestoreRevisionAsync(SelectedRevision.RevisionId);
            StatusMessage = $"Successfully restored rule to Version {SelectedRevision.VersionNumber}!";
            await LoadRevisionsAsync();
            OnRestoredCallback?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to restore revision: {ex.Message}";
        }
    }
}

