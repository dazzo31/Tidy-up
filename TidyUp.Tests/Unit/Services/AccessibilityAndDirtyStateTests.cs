using System.Collections.ObjectModel;
using System.Text.Json;
using System.Windows;
using FluentAssertions;
using Moq;
using TidyUp.Converters;
using TidyUp.Data.Repositories;
using TidyUp.Models;
using TidyUp.Models.Domain;
using TidyUp.Services.Rules;
using TidyUp.Services.Simulation;
using TidyUp.Services.Validation;
using TidyUp.ViewModels.RuleEditor;
using Xunit;

namespace TidyUp.Tests.Unit.Services;

public class AccessibilityAndDirtyStateTests
{
    #region AppSettings Window Placement Persistence

    [Fact]
    public void AppSettings_WindowGeometryDefaults_AreReasonable()
    {
        var settings = AppSettings.CreateDefault();

        settings.WindowState.Should().Be("Normal");
        settings.WindowWidth.Should().BeNull();
        settings.WindowHeight.Should().BeNull();
        settings.WindowLeft.Should().BeNull();
        settings.WindowTop.Should().BeNull();
    }

    [Fact]
    public void AppSettings_WindowGeometry_SerializesAndDeserializesCorrectly()
    {
        var settings = new AppSettings
        {
            WindowWidth = 1440,
            WindowHeight = 900,
            WindowLeft = 120,
            WindowTop = 80,
            WindowState = "Maximized"
        };

        var json = JsonSerializer.Serialize(settings);
        var restored = JsonSerializer.Deserialize<AppSettings>(json);

        restored.Should().NotBeNull();
        restored!.WindowWidth.Should().Be(1440);
        restored.WindowHeight.Should().Be(900);
        restored.WindowLeft.Should().Be(120);
        restored.WindowTop.Should().Be(80);
        restored.WindowState.Should().Be("Maximized");
    }

    #endregion

    #region CountToVisibilityConverter Tests

    [Theory]
    [InlineData(0, null, Visibility.Collapsed)]
    [InlineData(1, null, Visibility.Visible)]
    [InlineData(5, null, Visibility.Visible)]
    [InlineData(0, "Zero", Visibility.Visible)]
    [InlineData(1, "Zero", Visibility.Collapsed)]
    [InlineData(0, "Empty", Visibility.Visible)]
    [InlineData(3, "Empty", Visibility.Collapsed)]
    [InlineData(0, "Invert", Visibility.Visible)]
    [InlineData(2, "Invert", Visibility.Collapsed)]
    public void CountToVisibilityConverter_ConvertsIntegerCountsCorrectly(int count, string? parameter, Visibility expected)
    {
        var converter = new CountToVisibilityConverter();
        var result = converter.Convert(count, typeof(Visibility), parameter, System.Globalization.CultureInfo.InvariantCulture);

        result.Should().Be(expected);
    }

    [Fact]
    public void CountToVisibilityConverter_ConvertsCollectionsCorrectly()
    {
        var converter = new CountToVisibilityConverter();
        var emptyList = new List<string>();
        var populatedList = new List<string> { "item1", "item2" };

        converter.Convert(emptyList, typeof(Visibility), null, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Collapsed);

        converter.Convert(emptyList, typeof(Visibility), "Zero", System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Visible);

        converter.Convert(populatedList, typeof(Visibility), null, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Visible);

        converter.Convert(populatedList, typeof(Visibility), "Zero", System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be(Visibility.Collapsed);
    }

    #endregion

    #region RuleWizard Dirty Tracking & Discard Confirmation

    [Fact]
    public void RuleWizardViewModel_InitialState_IsNotDirty()
    {
        var mockRepo = new Mock<IRuleRepository>();
        var mockValidator = new Mock<IRuleValidator>();
        var mockPlanner = new Mock<IExecutionPlanGenerator>();

        var wizardVm = new RuleWizardViewModel(mockRepo.Object, mockValidator.Object, mockPlanner.Object);

        wizardVm.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void RuleWizardViewModel_EditingRuleName_SetsIsDirty()
    {
        var mockRepo = new Mock<IRuleRepository>();
        var mockValidator = new Mock<IRuleValidator>();
        var mockPlanner = new Mock<IExecutionPlanGenerator>();

        var wizardVm = new RuleWizardViewModel(mockRepo.Object, mockValidator.Object, mockPlanner.Object);

        wizardVm.RuleName = "Test Rule";
        wizardVm.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void RuleWizardViewModel_AddingMonitoredFolder_SetsIsDirty()
    {
        var mockRepo = new Mock<IRuleRepository>();
        var mockValidator = new Mock<IRuleValidator>();
        var mockPlanner = new Mock<IExecutionPlanGenerator>();

        var wizardVm = new RuleWizardViewModel(mockRepo.Object, mockValidator.Object, mockPlanner.Object);

        wizardVm.MonitoredFolders.Add(new MonitoredFolder { Path = @"C:\Test" });
        wizardVm.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void RuleWizardViewModel_AddingAction_SetsIsDirty()
    {
        var mockRepo = new Mock<IRuleRepository>();
        var mockValidator = new Mock<IRuleValidator>();
        var mockPlanner = new Mock<IExecutionPlanGenerator>();

        var wizardVm = new RuleWizardViewModel(mockRepo.Object, mockValidator.Object, mockPlanner.Object);

        wizardVm.ActionEditor.Actions.Add(new MoveFileAction());
        wizardVm.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void RuleWizardViewModel_CancelWhenDirty_AbortsWhenConfirmationDeclined()
    {
        var mockRepo = new Mock<IRuleRepository>();
        var mockValidator = new Mock<IRuleValidator>();
        var mockPlanner = new Mock<IExecutionPlanGenerator>();

        var wizardVm = new RuleWizardViewModel(mockRepo.Object, mockValidator.Object, mockPlanner.Object);
        wizardVm.RuleName = "Pending Rule";

        bool closeRequested = false;
        wizardVm.RequestClose += () => closeRequested = true;

        // User chooses "No" (don't discard)
        wizardVm.ConfirmDiscardCallback = (msg, title) => false;

        wizardVm.Cancel();

        closeRequested.Should().BeFalse();
    }

    [Fact]
    public void RuleWizardViewModel_CancelWhenDirty_ClosesWhenConfirmationAccepted()
    {
        var mockRepo = new Mock<IRuleRepository>();
        var mockValidator = new Mock<IRuleValidator>();
        var mockPlanner = new Mock<IExecutionPlanGenerator>();

        var wizardVm = new RuleWizardViewModel(mockRepo.Object, mockValidator.Object, mockPlanner.Object);
        wizardVm.RuleName = "Pending Rule";

        bool closeRequested = false;
        wizardVm.RequestClose += () => closeRequested = true;

        // User chooses "Yes" (discard)
        wizardVm.ConfirmDiscardCallback = (msg, title) => true;

        wizardVm.Cancel();

        closeRequested.Should().BeTrue();
    }

    [Fact]
    public void RuleWizardViewModel_CancelWhenClean_ClosesWithoutPrompt()
    {
        var mockRepo = new Mock<IRuleRepository>();
        var mockValidator = new Mock<IRuleValidator>();
        var mockPlanner = new Mock<IExecutionPlanGenerator>();

        var wizardVm = new RuleWizardViewModel(mockRepo.Object, mockValidator.Object, mockPlanner.Object);

        bool promptShown = false;
        wizardVm.ConfirmDiscardCallback = (msg, title) =>
        {
            promptShown = true;
            return false;
        };

        bool closeRequested = false;
        wizardVm.RequestClose += () => closeRequested = true;

        wizardVm.Cancel();

        promptShown.Should().BeFalse();
        closeRequested.Should().BeTrue();
    }

    #endregion
}
