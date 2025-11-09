using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;

namespace TidyUp.ViewModels;

/// <summary>
/// ViewModel for editing condition trees.
/// </summary>
public partial class ConditionEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private ConditionGroup? _rootCondition;

    [ObservableProperty]
    private object? _selectedItem;

    public ConditionEditorViewModel()
    {
        // Initialize with empty root group
        RootCondition = new ConditionGroup { Operator = LogicOperator.And };
    }

    /// <summary>
    /// Adds a new condition to the selected group.
    /// </summary>
    [RelayCommand]
    private void AddCondition(ConditionGroup? targetGroup = null)
    {
        var group = targetGroup ?? RootCondition;
        if (group == null) return;

        // Add a default file name condition
        var newCondition = new FileNameCondition
        {
            Operator = StringOperator.Contains,
            Value = ""
        };

        group.Conditions.Add(newCondition);
        SelectedItem = newCondition;
    }

    /// <summary>
    /// Adds a new condition group (AND/OR).
    /// </summary>
    [RelayCommand]
    private void AddGroup(ConditionGroup? targetGroup = null)
    {
        var group = targetGroup ?? RootCondition;
        if (group == null) return;

        var newGroup = new ConditionGroup
        {
            Operator = LogicOperator.And
        };

        group.Conditions.Add(newGroup);
        SelectedItem = newGroup;
    }

    /// <summary>
    /// Removes the selected condition or group.
    /// </summary>
    [RelayCommand]
    private void RemoveCondition(Condition? condition)
    {
        if (condition == null || RootCondition == null) return;

        RemoveConditionRecursive(RootCondition, condition);
    }

    private bool RemoveConditionRecursive(ConditionGroup group, Condition target)
    {
        if (group.Conditions.Remove(target))
            return true;

        foreach (var condition in group.Conditions.OfType<ConditionGroup>())
        {
            if (RemoveConditionRecursive(condition, target))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Changes the condition type (FileName, Extension, Size, Date).
    /// </summary>
    [RelayCommand]
    private void ChangeConditionType(string conditionType)
    {
        if (SelectedItem is not Condition selectedCondition) return;
        if (RootCondition == null) return;

        Condition newCondition = conditionType switch
        {
            "FileName" => new FileNameCondition { Operator = StringOperator.Contains, Value = "" },
            "Extension" => new FileExtensionCondition { Operator = StringOperator.Is, Value = "" },
            "Size" => new FileSizeCondition { Operator = FileSizeCondition.SizeOperator.GreaterThan, Value = 0 },
            "Date" => new FileDateCondition 
            { 
                Type = FileDateCondition.DateType.Modified,
                Operator = FileDateCondition.DateOperator.After,
                Value = DateTime.Today
            },
            _ => selectedCondition
        };

        ReplaceConditionRecursive(RootCondition, selectedCondition, newCondition);
        SelectedItem = newCondition;
    }

    private bool ReplaceConditionRecursive(ConditionGroup group, Condition oldCondition, Condition newCondition)
    {
        var index = group.Conditions.IndexOf(oldCondition);
        if (index >= 0)
        {
            group.Conditions[index] = newCondition;
            return true;
        }

        foreach (var condition in group.Conditions.OfType<ConditionGroup>())
        {
            if (ReplaceConditionRecursive(condition, oldCondition, newCondition))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Gets the display name for a condition type.
    /// </summary>
    public static string GetConditionTypeName(Condition condition)
    {
        return condition switch
        {
            FileNameCondition => "File Name",
            FileExtensionCondition => "Extension",
            FileSizeCondition => "File Size",
            FileDateCondition => "Date",
            ConditionGroup => "Group",
            _ => "Unknown"
        };
    }
}
