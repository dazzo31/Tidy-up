using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using TidyUp.Models.Domain;
using TidyUp.Services.Rules;

namespace TidyUp.ViewModels;

public partial class RuleConflictsViewModel : ObservableObject
{
    [ObservableProperty]
    private MultiRuleConflictReport _report;

    [ObservableProperty]
    private ObservableCollection<RuleConflict> _conflicts;

    [ObservableProperty]
    private RuleConflict? _selectedConflict;

    public bool HasConflicts => Report.HasConflicts;

    public string SummaryText => HasConflicts
        ? $"Found {Report.Conflicts.Count} rule conflict(s) across {Report.TotalRulesAnalyzed} active rules."
        : $"No conflicts detected across {Report.TotalRulesAnalyzed} active rules. Rules execute harmoniously.";

    public RuleConflictsViewModel(MultiRuleConflictReport? report = null)
    {
        _report = report ?? new MultiRuleConflictReport();
        _conflicts = new ObservableCollection<RuleConflict>(_report.Conflicts);
    }
}
