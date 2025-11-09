using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyUp.Data;
using TidyUp.Data.Repositories;
using TidyUp.Models;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;

namespace TidyUp.ViewModels;

public partial class FirstRunViewModel : ObservableObject
{
    private readonly SettingsRepository _settingsRepository;
    private readonly IRuleRepository _ruleRepository;
    private readonly AppSettings _settings;

    [ObservableProperty]
    private int _currentStep = 1;

    [ObservableProperty]
    private bool _createSampleRule = true;

    [ObservableProperty]
    private ObservableCollection<ConflictResolution> _availableConflictStrategies;

    [ObservableProperty]
    private ConflictResolution _selectedConflictStrategy;

    [ObservableProperty]
    private bool _enableSystemTray;

    [ObservableProperty]
    private bool _enableNotifications;

    [ObservableProperty]
    private bool _showThisAgain = false;

    public FirstRunViewModel(SettingsRepository settingsRepository, IRuleRepository ruleRepository, AppSettings settings)
    {
        _settingsRepository = settingsRepository;
        _ruleRepository = ruleRepository;
        _settings = settings;

        // Initialize conflict strategies
        _availableConflictStrategies = new ObservableCollection<ConflictResolution>(
            Enum.GetValues<ConflictResolution>()
        );

        // Load current settings
        _selectedConflictStrategy = settings.DefaultConflictResolution;
        _enableSystemTray = settings.SystemTrayEnabled;
        _enableNotifications = settings.ToastNotificationsEnabled;
        _showThisAgain = settings.ShowFirstRunWizard;
    }

    [RelayCommand]
    private void Next()
    {
        if (CurrentStep < 5)
        {
            CurrentStep++;
        }
    }

    [RelayCommand]
    private void Previous()
    {
        if (CurrentStep > 1)
        {
            CurrentStep--;
        }
    }

    [RelayCommand]
    private async Task FinishAsync()
    {
        // Apply settings
        _settings.DefaultConflictResolution = SelectedConflictStrategy;
        _settings.SystemTrayEnabled = EnableSystemTray;
        _settings.ToastNotificationsEnabled = EnableNotifications;
        _settings.ShowFirstRunWizard = ShowThisAgain;

        await _settingsRepository.SaveAsync(_settings);

        // Create sample rule if requested
        if (CreateSampleRule)
        {
            await CreateSampleRuleAsync();
        }

        // Close the window
        OnFinishRequested?.Invoke();
    }

    private async Task CreateSampleRuleAsync()
    {
        try
        {
            var downloadsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            var sampleRule = new Rule
            {
                Name = "Organize Documents by Type",
                Description = "Automatically sorts PDF, Word, and Excel files from Downloads into organized folders. DISABLED by default - enable after reviewing!",
                IsEnabled = false, // Disabled for safety
                ExecutionOrder = 0,
                StopProcessingAfterMatch = false
            };

            // Add monitored folder
            sampleRule.MonitoredFolders.Add(new MonitoredFolder
            {
                Path = downloadsPath,
                IncludeSubfolders = false
            });

            // Add condition group (PDF OR Word OR Excel)
            var conditionGroup = new ConditionGroup
            {
                Operator = LogicOperator.Or
            };
            conditionGroup.Conditions.Add(new FileExtensionCondition
            {
                Value = ".pdf",
                Operator = StringOperator.Is
            });
            conditionGroup.Conditions.Add(new FileExtensionCondition
            {
                Value = ".docx",
                Operator = StringOperator.Is
            });
            conditionGroup.Conditions.Add(new FileExtensionCondition
            {
                Value = ".xlsx",
                Operator = StringOperator.Is
            });

            sampleRule.Conditions = conditionGroup;

            // Add move action with variable-based path
            sampleRule.Actions.Add(new MoveFileAction
            {
                DestinationPath = Path.Combine(documentsPath, "{FileExtension}", "{Year}-{Month}"),
                ConflictResolution = ConflictResolution.RenameNew,
                Order = 0
            });

            // Save to database
            await _ruleRepository.AddAsync(sampleRule);
        }
        catch (Exception ex)
        {
            // Log error but don't fail the wizard
            Console.WriteLine($"Error creating sample rule: {ex.Message}");
        }
    }

    public Action? OnFinishRequested { get; set; }
}
