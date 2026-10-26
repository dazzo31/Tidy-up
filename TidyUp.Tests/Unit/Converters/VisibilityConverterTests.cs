using System.Globalization;
using System.Windows;
using FluentAssertions;
using TidyUp.Converters;
using TidyUp.Models.Domain;
using TidyUp.ViewModels.RuleEditor;
using Xunit;

namespace TidyUp.Tests.Unit.Converters;

public class VisibilityConverterTests
{
    private readonly InverseBooleanToVisibilityConverter _inverseBoolConverter = new();
    private readonly NullToVisibilityConverter _nullConverter = new();

    [Theory]
    [InlineData(true, Visibility.Collapsed)]
    [InlineData(false, Visibility.Visible)]
    public void InverseBooleanToVisibilityConverter_ConvertsCorrectly(bool input, Visibility expected)
    {
        var result = _inverseBoolConverter.Convert(input, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Fact]
    public void InverseBooleanToVisibilityConverter_WhenNonBoolean_DefaultsToVisible()
    {
        var result = _inverseBoolConverter.Convert("not a bool", typeof(Visibility), null!, CultureInfo.InvariantCulture);
        result.Should().Be(Visibility.Visible);
    }

    [Theory]
    [InlineData(Visibility.Collapsed, true)]
    [InlineData(Visibility.Hidden, true)]
    [InlineData(Visibility.Visible, false)]
    public void InverseBooleanToVisibilityConverter_ConvertBack_ReturnsExpectedBool(Visibility input, bool expected)
    {
        var result = _inverseBoolConverter.ConvertBack(input, typeof(bool), null!, CultureInfo.InvariantCulture);
        result.Should().Be(expected);
    }

    [Fact]
    public void NullToVisibilityConverter_WhenNull_ReturnsCollapsed()
    {
        var result = _nullConverter.Convert(null!, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        result.Should().Be(Visibility.Collapsed);
    }

    [Fact]
    public void NullToVisibilityConverter_WhenNotNull_ReturnsVisible()
    {
        var plan = new ExecutionPlan();
        var result = _nullConverter.Convert(plan, typeof(Visibility), null!, CultureInfo.InvariantCulture);
        result.Should().Be(Visibility.Visible);
    }

    [Fact]
    public void RuleWizardViewModel_ExecutionPlanChanges_RaiseHasExecutionPlanNotification()
    {
        var mockRepo = new Moq.Mock<TidyUp.Data.Repositories.IRuleRepository>();
        var mockValidator = new Moq.Mock<TidyUp.Services.Validation.IRuleValidator>();
        var mockPlanGen = new Moq.Mock<TidyUp.Services.Simulation.IExecutionPlanGenerator>();
        var mockSummary = new Moq.Mock<TidyUp.Services.Rules.IRuleSummaryGenerator>();

        var vm = new RuleWizardViewModel(mockRepo.Object, mockValidator.Object, mockPlanGen.Object, mockSummary.Object);

        vm.HasExecutionPlan.Should().BeFalse();

        bool notified = false;
        vm.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(RuleWizardViewModel.HasExecutionPlan))
                notified = true;
        };

        vm.ExecutionPlan = new ExecutionPlan();

        vm.HasExecutionPlan.Should().BeTrue();
        notified.Should().BeTrue();

        vm.ExecutionPlan = null;
        vm.HasExecutionPlan.Should().BeFalse();
    }
}
