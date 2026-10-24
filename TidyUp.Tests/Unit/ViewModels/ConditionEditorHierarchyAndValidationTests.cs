using System.IO;
using FluentAssertions;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

public class ConditionEditorHierarchyAndValidationTests
{
    private readonly ConditionEditorViewModel _viewModel;

    public ConditionEditorHierarchyAndValidationTests()
    {
        _viewModel = new ConditionEditorViewModel();
    }

    [Fact]
    public void FileNameCondition_WithValidRegex_HasNoError()
    {
        var condition = new FileNameCondition
        {
            Operator = StringOperator.MatchesRegex,
            Value = @"^Invoice_\d{4}$"
        };

        condition.IsRegexOperator.Should().BeTrue();
        condition.HasRegexError.Should().BeFalse();
        condition.RegexErrorMessage.Should().BeEmpty();
    }

    [Fact]
    public void FileNameCondition_WithInvalidRegex_ExposesErrorDetails()
    {
        var condition = new FileNameCondition
        {
            Operator = StringOperator.MatchesRegex,
            Value = @"[a-z("
        };

        condition.IsRegexOperator.Should().BeTrue();
        condition.HasRegexError.Should().BeTrue();
        condition.RegexErrorMessage.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void FileNameCondition_Evaluate_WithInvalidRegex_DoesNotThrowAndReturnsFalse()
    {
        var condition = new FileNameCondition
        {
            Operator = StringOperator.MatchesRegex,
            Value = @"[invalid(regex"
        };

        var file = new FileInfo(@"C:\Test\testfile.txt");
        bool result = false;
        var act = () => { result = condition.Evaluate(file); };

        act.Should().NotThrow();
        result.Should().BeFalse();
    }

    [Fact]
    public void FileNameCondition_Evaluate_WithValidRegex_MatchesCorrectly()
    {
        var condition = new FileNameCondition
        {
            Operator = StringOperator.MatchesRegex,
            Value = @"^report_\d{3}$"
        };

        var matching = new FileInfo(@"C:\Test\report_123.txt");
        var nonMatching = new FileInfo(@"C:\Test\invoice_123.txt");

        condition.Evaluate(matching).Should().BeTrue();
        condition.Evaluate(nonMatching).Should().BeFalse();
    }

    [Fact]
    public void FileSizeCondition_DisplaySizeAndUnits_ConvertCorrectly()
    {
        var condition = new FileSizeCondition();

        // 5 MB
        condition.SizeUnit = "MB";
        condition.DisplaySize = 5;

        condition.Value.Should().Be(5L * 1024 * 1024);

        // Switch to KB
        condition.SizeUnit = "KB";
        condition.DisplaySize = 500;
        condition.Value.Should().Be(500L * 1024);

        // Switch to GB
        condition.SizeUnit = "GB";
        condition.DisplaySize = 2;
        condition.Value.Should().Be(2L * 1024 * 1024 * 1024);
    }

    [Fact]
    public void FileSizeCondition_SettingBytes_SynchronizesDisplayAndUnit()
    {
        var condition = new FileSizeCondition
        {
            Value = 10L * 1024 * 1024
        };

        condition.DisplaySize.Should().Be(10);
        condition.SizeUnit.Should().Be("MB");
    }

    [Fact]
    public void FileDateCondition_Operator_UpdatesHelperFlags()
    {
        var condition = new FileDateCondition
        {
            Operator = FileDateCondition.DateOperator.OlderThanDays
        };

        condition.IsOlderThanDays.Should().BeTrue();
        condition.IsBetween.Should().BeFalse();
        condition.IsSpecificDate.Should().BeFalse();

        condition.Operator = FileDateCondition.DateOperator.Between;
        condition.IsBetween.Should().BeTrue();
        condition.IsOlderThanDays.Should().BeFalse();
        condition.IsSpecificDate.Should().BeTrue();

        condition.Operator = FileDateCondition.DateOperator.Before;
        condition.IsBetween.Should().BeFalse();
        condition.IsOlderThanDays.Should().BeFalse();
        condition.IsSpecificDate.Should().BeTrue();
    }

    [Fact]
    public void ConditionEditor_NestedGroups_AddAndEvaluateProperly()
    {
        _viewModel.RootCondition = new ConditionGroup { Operator = LogicOperator.And };

        // Add child group inside root
        _viewModel.AddGroupCommand.Execute(_viewModel.RootCondition);
        _viewModel.RootCondition.Conditions.Should().ContainSingle();
        var childGroup = _viewModel.RootCondition.Conditions[0] as ConditionGroup;
        childGroup.Should().NotBeNull();
        childGroup!.Operator = LogicOperator.Or;

        // Add condition inside child group
        _viewModel.AddConditionCommand.Execute(childGroup);
        childGroup.Conditions.Should().ContainSingle();
        var cond = childGroup.Conditions[0] as FileNameCondition;
        cond.Should().NotBeNull();
        cond!.Value = "test";

        childGroup.DisplaySummary.Should().Contain("Or");
        childGroup.DisplaySummary.Should().Contain("1 conditions");
    }

    [Fact]
    public void ConditionEditor_DeleteGroupWithConditions_PromptsUserAndCancelsWhenRefused()
    {
        _viewModel.RootCondition = new ConditionGroup { Operator = LogicOperator.And };
        _viewModel.AddGroupCommand.Execute(_viewModel.RootCondition);
        var childGroup = (ConditionGroup)_viewModel.RootCondition.Conditions[0];
        _viewModel.AddConditionCommand.Execute(childGroup);

        bool promptWasCalled = false;
        _viewModel.ConfirmDeleteGroupCallback = (msg, title) =>
        {
            promptWasCalled = true;
            return false; // User cancels
        };

        _viewModel.RemoveConditionCommand.Execute(childGroup);

        promptWasCalled.Should().BeTrue();
        _viewModel.RootCondition.Conditions.Should().Contain(childGroup); // preserved
    }

    [Fact]
    public void ConditionEditor_DeleteGroupWithConditions_DeletesWhenConfirmed()
    {
        _viewModel.RootCondition = new ConditionGroup { Operator = LogicOperator.And };
        _viewModel.AddGroupCommand.Execute(_viewModel.RootCondition);
        var childGroup = (ConditionGroup)_viewModel.RootCondition.Conditions[0];
        _viewModel.AddConditionCommand.Execute(childGroup);

        _viewModel.ConfirmDeleteGroupCallback = (msg, title) => true; // User confirms

        _viewModel.RemoveConditionCommand.Execute(childGroup);

        _viewModel.RootCondition.Conditions.Should().BeEmpty();
    }

    [Fact]
    public void ConditionEditor_DeleteEmptyGroup_DeletesImmediatelyWithoutPrompt()
    {
        _viewModel.RootCondition = new ConditionGroup { Operator = LogicOperator.And };
        _viewModel.AddGroupCommand.Execute(_viewModel.RootCondition);
        var childGroup = (ConditionGroup)_viewModel.RootCondition.Conditions[0];

        bool promptWasCalled = false;
        _viewModel.ConfirmDeleteGroupCallback = (msg, title) =>
        {
            promptWasCalled = true;
            return true;
        };

        _viewModel.RemoveConditionCommand.Execute(childGroup);

        promptWasCalled.Should().BeFalse(); // No prompt for empty group
        _viewModel.RootCondition.Conditions.Should().BeEmpty();
    }

    [Fact]
    public void ConditionEditor_ChangeConditionType_ReplacesInPlaceAndPreservesValue()
    {
        _viewModel.RootCondition = new ConditionGroup { Operator = LogicOperator.And };
        var c1 = new FileNameCondition { Value = "first" };
        var c2 = new FileNameCondition { Value = "pdf" };
        var c3 = new FileNameCondition { Value = "third" };
        _viewModel.RootCondition.Conditions.Add(c1);
        _viewModel.RootCondition.Conditions.Add(c2);
        _viewModel.RootCondition.Conditions.Add(c3);

        _viewModel.ChangeConditionType(c2, "Extension");

        _viewModel.RootCondition.Conditions.Count.Should().Be(3);
        _viewModel.RootCondition.Conditions[0].Should().BeSameAs(c1);
        _viewModel.RootCondition.Conditions[2].Should().BeSameAs(c3);

        var replaced = _viewModel.RootCondition.Conditions[1] as FileExtensionCondition;
        replaced.Should().NotBeNull();
        replaced!.Value.Should().Be("pdf");
        replaced.Operator.Should().Be(StringOperator.Is);
    }

    [Fact]
    public void ConditionEditor_ChangeConditionType_InsideNestedGroup_ReplacesCorrectly()
    {
        _viewModel.RootCondition = new ConditionGroup { Operator = LogicOperator.And };
        var nested = new ConditionGroup { Operator = LogicOperator.Or };
        var fn = new FileNameCondition { Value = "temp" };
        nested.Conditions.Add(fn);
        _viewModel.RootCondition.Conditions.Add(nested);

        _viewModel.ChangeConditionType(fn, "Size");

        nested.Conditions.Count.Should().Be(1);
        var sizeCondition = nested.Conditions[0] as FileSizeCondition;
        sizeCondition.Should().NotBeNull();
        sizeCondition!.Operator.Should().Be(FileSizeCondition.SizeOperator.GreaterThan);
        sizeCondition.Value.Should().Be(0);
    }

    [Fact]
    public void ConditionEditor_ChangeConditionType_ToDateCondition_SetsDefaultDateProperties()
    {
        _viewModel.RootCondition = new ConditionGroup { Operator = LogicOperator.And };
        var fn = new FileNameCondition { Value = "old" };
        _viewModel.RootCondition.Conditions.Add(fn);

        _viewModel.ChangeConditionType(fn, "Date");

        var dateCondition = _viewModel.RootCondition.Conditions[0] as FileDateCondition;
        dateCondition.Should().NotBeNull();
        dateCondition!.Type.Should().Be(FileDateCondition.DateType.Modified);
        dateCondition.Operator.Should().Be(FileDateCondition.DateOperator.After);
        dateCondition.Value.Date.Should().Be(DateTime.Today);
    }

    [Fact]
    public void ConditionEditor_ChangeConditionType_SameType_DoesNotReplace()
    {
        _viewModel.RootCondition = new ConditionGroup { Operator = LogicOperator.And };
        var fn = new FileNameCondition { Value = "report" };
        _viewModel.RootCondition.Conditions.Add(fn);

        _viewModel.ChangeConditionType(fn, "FileName");

        _viewModel.RootCondition.Conditions[0].Should().BeSameAs(fn);
    }
}
