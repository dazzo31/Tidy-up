using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyUp.Services.Rules;

namespace TidyUp.ViewModels;

public partial class RuleConflictsViewModel : ObservableObject
{
    [ObservableProperty]
    private MultiRuleConflictReport _report;

    [ObservableProperty]
    private ObservableCollection<RuleConflict> _conflicts = new();

    [ObservableProperty]
    private RuleConflict? _selectedConflict;

    public bool HasConflicts => Report.HasConflicts;

    public string SummaryText => HasConflicts
        ? $"Found {Report.Conflicts.Count} rule conflict(s) across {Report.TotalRulesAnalyzed} active rules."
        : $"No conflicts detected across {Report.TotalRulesAnalyzed} active rules. Rules execute harmoniously.";

    public RuleConflictsViewModel(MultiRuleConflictReport report)
    {
        _report = report ?? throw new ArgumentNullException(nameof(report));
        _conflicts = new ObservableCollection<RuleConflict>(report.Conflicts);
    }
}

