using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Services;
using TidyUp.Services.Diagnostics;

namespace TidyUp.ViewModels;

/// <summary>
/// ViewModel for the Rule Evaluation Inspector / Condition Inspector dialog.
/// Allows users and developers to test rules against specific files and view
/// condition-by-condition diagnostic evaluations and explanations.
/// </summary>
public partial class RuleEvaluationInspectorViewModel : ObservableObject
{
    private readonly IRuleEngine _ruleEngine;
    private readonly IRuleRepository? _ruleRepository;

    public ObservableCollection<Rule> AvailableRules { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInspect))]
    private Rule? _selectedRule;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInspect))]
    private string? _targetFilePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasDiagnostics))]
    [NotifyPropertyChangedFor(nameof(ResultSummary))]
    [NotifyPropertyChangedFor(nameof(IsMatch))]
    private FileEvaluationDiagnostics? _diagnostics;

    [ObservableProperty]
    private bool _isEvaluating;

    public bool HasDiagnostics => Diagnostics != null;

    public string ResultSummary => Diagnostics?.Summary ?? "No evaluation performed yet.";

    public bool IsMatch => Diagnostics?.IsOverallMatch ?? false;

    public bool CanInspect => SelectedRule != null && !string.IsNullOrWhiteSpace(TargetFilePath);

    public RuleEvaluationInspectorViewModel(
        IRuleEngine ruleEngine,
        IRuleRepository? ruleRepository = null)
    {
        _ruleEngine = ruleEngine;
        _ruleRepository = ruleRepository;
    }

    /// <summary>
    /// Loads all configured rules from the repository into the rule selection list.
    /// </summary>
    [RelayCommand]
    public async Task LoadRulesAsync()
    {
        if (_ruleRepository == null)
            return;

        try
        {
            var rules = await _ruleRepository.GetAllAsync();
            AvailableRules.Clear();
            foreach (var rule in rules)
            {
                AvailableRules.Add(rule);
            }

            if (SelectedRule == null && AvailableRules.Count > 0)
            {
                SelectedRule = AvailableRules[0];
            }
        }
        catch
        {
            // Suppress repository read errors during inspector load
        }
    }

    /// <summary>
    /// Evaluates the currently selected rule against the target file path.
    /// </summary>
    [RelayCommand]
    public void InspectFile(string? explicitPath = null)
    {
        var path = explicitPath ?? TargetFilePath;
        if (SelectedRule == null || string.IsNullOrWhiteSpace(path))
            return;

        IsEvaluating = true;
        try
        {
            Diagnostics = _ruleEngine.ExplainEvaluation(SelectedRule, path);
        }
        finally
        {
            IsEvaluating = false;
        }
    }

    /// <summary>
    /// Clears the currently displayed diagnostic results.
    /// </summary>
    [RelayCommand]
    public void ClearDiagnostics()
    {
        Diagnostics = null;
    }
}

