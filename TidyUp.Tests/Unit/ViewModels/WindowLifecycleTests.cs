using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using TidyUp.Models;
using TidyUp.Services.Lifecycle;
using TidyUp.Services.State;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

public class WindowLifecycleTests
{
    private readonly Mock<IApplicationStateManager> _stateManagerMock = new();

    [Fact]
    public void EvaluateCloseAction_ReturnsCancel_WhenUserDeclinesDiscardingDirtyEdits()
    {
        var settings = new AppSettings { MinimizeToTray = true, SystemTrayEnabled = true };
        var coordinator = new WindowCloseCoordinator(settings, _stateManagerMock.Object);

        var action = coordinator.EvaluateCloseAction(
            isRuleDirty: true,
            confirmDiscardEdits: () => false, // User declines discard
            confirmWaitBatch: null,
            isExplicitExit: false);

        action.Should().Be(WindowCloseAction.Cancel);
    }

    [Fact]
    public void EvaluateCloseAction_ReturnsCancel_WhenBatchExecutionIsActive_AndUserChoosesToWait()
    {
        var settings = new AppSettings { MinimizeToTray = true, SystemTrayEnabled = true };
        _stateManagerMock.Setup(s => s.CurrentState).Returns(ApplicationLifecycleState.ExecutingBatch);
        var coordinator = new WindowCloseCoordinator(settings, _stateManagerMock.Object);

        var action = coordinator.EvaluateCloseAction(
            isRuleDirty: false,
            confirmDiscardEdits: null,
            confirmWaitBatch: (msg, title) => true, // User chooses to wait
            isExplicitExit: false);

        action.Should().Be(WindowCloseAction.Cancel);
    }

    [Fact]
    public void EvaluateCloseAction_ReturnsMinimizeToTray_WhenMinimizeToTrayIsTrue_AndClean()
    {
        var settings = new AppSettings { MinimizeToTray = true, SystemTrayEnabled = true };
        _stateManagerMock.Setup(s => s.CurrentState).Returns(ApplicationLifecycleState.Idle);
        var coordinator = new WindowCloseCoordinator(settings, _stateManagerMock.Object);

        var action = coordinator.EvaluateCloseAction(
            isRuleDirty: false,
            confirmDiscardEdits: null,
            confirmWaitBatch: null,
            isExplicitExit: false);

        action.Should().Be(WindowCloseAction.MinimizeToTray);
    }

    [Fact]
    public void EvaluateCloseAction_ReturnsCloseAndExit_WhenMinimizeToTrayIsFalse()
    {
        var settings = new AppSettings { MinimizeToTray = false, SystemTrayEnabled = true };
        _stateManagerMock.Setup(s => s.CurrentState).Returns(ApplicationLifecycleState.Idle);
        var coordinator = new WindowCloseCoordinator(settings, _stateManagerMock.Object);

        var action = coordinator.EvaluateCloseAction(
            isRuleDirty: false,
            confirmDiscardEdits: null,
            confirmWaitBatch: null,
            isExplicitExit: false);

        action.Should().Be(WindowCloseAction.CloseAndExit);
    }

    [Fact]
    public void EvaluateCloseAction_ReturnsCloseAndExit_WhenSystemTrayIsDisabled()
    {
        var settings = new AppSettings { MinimizeToTray = true, SystemTrayEnabled = false };
        _stateManagerMock.Setup(s => s.CurrentState).Returns(ApplicationLifecycleState.Idle);
        var coordinator = new WindowCloseCoordinator(settings, _stateManagerMock.Object);

        var action = coordinator.EvaluateCloseAction(
            isRuleDirty: false,
            confirmDiscardEdits: null,
            confirmWaitBatch: null,
            isExplicitExit: false);

        action.Should().Be(WindowCloseAction.CloseAndExit);
    }

    [Fact]
    public void EvaluateCloseAction_ReturnsCloseAndExit_WhenExplicitExitIsTrue_EvenIfMinimizeToTrayIsTrue()
    {
        var settings = new AppSettings { MinimizeToTray = true, SystemTrayEnabled = true };
        _stateManagerMock.Setup(s => s.CurrentState).Returns(ApplicationLifecycleState.Idle);
        var coordinator = new WindowCloseCoordinator(settings, _stateManagerMock.Object);

        var action = coordinator.EvaluateCloseAction(
            isRuleDirty: false,
            confirmDiscardEdits: null,
            confirmWaitBatch: null,
            isExplicitExit: true); // Explicit application exit from menu or tray

        action.Should().Be(WindowCloseAction.CloseAndExit);
    }

    [Fact]
    public async Task RequestApplicationExitAsync_WaitsForActiveBatchToComplete()
    {
        var settings = new AppSettings();
        int pollCount = 0;
        _stateManagerMock.Setup(s => s.CurrentState).Returns(() =>
        {
            pollCount++;
            return pollCount > 2 ? ApplicationLifecycleState.Idle : ApplicationLifecycleState.ExecutingBatch;
        });

        var coordinator = new WindowCloseCoordinator(settings, _stateManagerMock.Object);

        var exitPermitted = await coordinator.RequestApplicationExitAsync(
            confirmDiscardEdits: () => true,
            confirmForceExit: null,
            timeout: TimeSpan.FromSeconds(2));

        exitPermitted.Should().BeTrue();
        pollCount.Should().BeGreaterThan(2);
    }

    [Fact]
    public async Task RequestApplicationExitAsync_Aborts_WhenUserDeclinesDiscardingEdits()
    {
        var settings = new AppSettings();
        _stateManagerMock.Setup(s => s.CurrentState).Returns(ApplicationLifecycleState.Idle);
        var coordinator = new WindowCloseCoordinator(settings, _stateManagerMock.Object);

        var exitPermitted = await coordinator.RequestApplicationExitAsync(
            confirmDiscardEdits: () => false, // User declines discard
            confirmForceExit: null);

        exitPermitted.Should().BeFalse();
    }

    [Fact]
    public async Task RequestApplicationExitAsync_PromptsForceExit_WhenBatchTimesOut()
    {
        var settings = new AppSettings();
        _stateManagerMock.Setup(s => s.CurrentState).Returns(ApplicationLifecycleState.ExecutingBatch);
        var coordinator = new WindowCloseCoordinator(settings, _stateManagerMock.Object);

        bool promptInvoked = false;
        var exitPermitted = await coordinator.RequestApplicationExitAsync(
            confirmDiscardEdits: () => true,
            confirmForceExit: (msg, title) =>
            {
                promptInvoked = true;
                return false; // User declines to force exit
            },
            timeout: TimeSpan.FromMilliseconds(200));

        promptInvoked.Should().BeTrue();
        exitPermitted.Should().BeFalse();
    }
}
