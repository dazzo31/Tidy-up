using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using TidyUp.Models.Enums;

namespace TidyUp.Models.Domain;

/// <summary>
/// Base class for all file conditions.
/// </summary>
public abstract partial class Condition : ObservableObject
{
    /// <summary>
    /// Unique identifier for the condition.
    /// </summary>
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    /// <summary>
    /// Evaluates whether the condition is met for the given file.
    /// </summary>
    public abstract bool Evaluate(FileInfo fileInfo);
}

/// <summary>
/// Groups multiple conditions with AND/OR logic.
/// </summary>
public partial class ConditionGroup : Condition
{
    /// <summary>
    /// Logical operator for combining child conditions.
    /// </summary>
    [ObservableProperty]
    private LogicOperator _operator = LogicOperator.And;

    /// <summary>
    /// Child conditions or condition groups.
    /// </summary>
    public ObservableCollection<Condition> Conditions { get; set; } = new();

    public override bool Evaluate(FileInfo fileInfo)
    {
        if (!Conditions.Any())
            return true; // Empty group matches all

        return Operator == LogicOperator.And
            ? Conditions.All(c => c.Evaluate(fileInfo))
            : Conditions.Any(c => c.Evaluate(fileInfo));
    }
}

/// <summary>
/// Condition that checks the file name.
/// </summary>
public partial class FileNameCondition : Condition
{
    [ObservableProperty]
    private StringOperator _operator = StringOperator.Is;
    
    [ObservableProperty]
    private string _value = string.Empty;

    public override bool Evaluate(FileInfo fileInfo)
    {
        var fileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
        return EvaluateString(fileName, Value, Operator);
    }

    private static bool EvaluateString(string actual, string expected, StringOperator op)
    {
        var comparison = StringComparison.OrdinalIgnoreCase;
        return op switch
        {
            StringOperator.Is => actual.Equals(expected, comparison),
            StringOperator.IsNot => !actual.Equals(expected, comparison),
            StringOperator.Contains => actual.Contains(expected, comparison),
            StringOperator.DoesNotContain => !actual.Contains(expected, comparison),
            StringOperator.StartsWith => actual.StartsWith(expected, comparison),
            StringOperator.EndsWith => actual.EndsWith(expected, comparison),
            StringOperator.MatchesRegex => System.Text.RegularExpressions.Regex.IsMatch(actual, expected),
            StringOperator.IsEmpty => string.IsNullOrWhiteSpace(actual),
            _ => false
        };
    }
}

/// <summary>
/// Condition that checks the file extension.
/// </summary>
public partial class FileExtensionCondition : Condition
{
    [ObservableProperty]
    private StringOperator _operator = StringOperator.Is;
    
    [ObservableProperty]
    private string _value = string.Empty;

    public override bool Evaluate(FileInfo fileInfo)
    {
        var extension = fileInfo.Extension.TrimStart('.');
        var expectedExtension = Value.TrimStart('.');
        
        return Operator switch
        {
            StringOperator.Is => extension.Equals(expectedExtension, StringComparison.OrdinalIgnoreCase),
            StringOperator.IsNot => !extension.Equals(expectedExtension, StringComparison.OrdinalIgnoreCase),
            StringOperator.Contains => extension.Contains(expectedExtension, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}

/// <summary>
/// Condition that checks the file size.
/// </summary>
public partial class FileSizeCondition : Condition
{
    public enum SizeOperator
    {
        Equals,
        NotEquals,
        GreaterThan,
        LessThan,
        Between
    }

    [ObservableProperty]
    private SizeOperator _operator = SizeOperator.GreaterThan;
    
    [ObservableProperty]
    private long _value; // Size in bytes
    
    [ObservableProperty]
    private long? _maxValue; // For "Between" operator

    public override bool Evaluate(FileInfo fileInfo)
    {
        var size = fileInfo.Length;
        return Operator switch
        {
            SizeOperator.Equals => size == Value,
            SizeOperator.NotEquals => size != Value,
            SizeOperator.GreaterThan => size > Value,
            SizeOperator.LessThan => size < Value,
            SizeOperator.Between => size >= Value && size <= (MaxValue ?? long.MaxValue),
            _ => false
        };
    }
}

/// <summary>
/// Condition that checks file dates (created or modified).
/// </summary>
public partial class FileDateCondition : Condition
{
    public enum DateType
    {
        Created,
        Modified
    }

    public enum DateOperator
    {
        Is,
        Before,
        After,
        Between,
        OlderThanDays
    }

    [ObservableProperty]
    private DateType _type = DateType.Modified;
    
    [ObservableProperty]
    private DateOperator _operator = DateOperator.After;
    
    [ObservableProperty]
    private DateTime _value = DateTime.Today;
    
    [ObservableProperty]
    private DateTime? _maxValue; // For "Between" operator
    
    [ObservableProperty]
    private int? _daysOld; // For "OlderThanDays" operator

    public override bool Evaluate(FileInfo fileInfo)
    {
        var date = Type == DateType.Created 
            ? fileInfo.CreationTime 
            : fileInfo.LastWriteTime;

        return Operator switch
        {
            DateOperator.Is => date.Date == Value.Date,
            DateOperator.Before => date < Value,
            DateOperator.After => date > Value,
            DateOperator.Between => date >= Value && date <= (MaxValue ?? DateTime.MaxValue),
            DateOperator.OlderThanDays => (DateTime.Now - date).TotalDays > (DaysOld ?? 0),
            _ => false
        };
    }
}
