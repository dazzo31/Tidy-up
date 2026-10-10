using FluentAssertions;
using TidyUp.Services.State;
using Xunit;

namespace TidyUp.Tests.Unit.Services;

public class ApplicationStateManagerTests
{
    private readonly ApplicationStateManager _stateManager;

    public ApplicationStateManagerTests()
    {
        _stateManager = new ApplicationStateManager();
    }

    #region Initial State & Valid Transitions

    [Fact]
    public void InitialState_IsIdle_AndRuleIsUnvalidated()
    {
        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.Idle);
        _stateManager.IsRuleValidated.Should().BeFalse();
        _stateManager.ActiveOperationCount.Should().Be(0);
    }

    [Fact]
    public void ValidTransitions_FollowLifecycleWorkflow()
    {
        // 1. Idle -> ConfiguringRule
        _stateManager.TryTransitionTo(ApplicationLifecycleState.ConfiguringRule).Should().BeTrue();
        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.ConfiguringRule);

        // 2. ConfiguringRule -> Simulating
        _stateManager.TryTransitionTo(ApplicationLifecycleState.Simulating).Should().BeTrue();
        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.Simulating);

        // 3. Simulating -> PreviewReady
        _stateManager.TryTransitionTo(ApplicationLifecycleState.PreviewReady).Should().BeTrue();
        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.PreviewReady);

        // 4. PreviewReady -> ExecutingBatch
        _stateManager.TryTransitionTo(ApplicationLifecycleState.ExecutingBatch).Should().BeTrue();
        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.ExecutingBatch);

        // 5. ExecutingBatch -> Idle
        _stateManager.TryTransitionTo(ApplicationLifecycleState.Idle).Should().BeTrue();
        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.Idle);
    }

    #endregion

    #region Illegal Transitions

    [Fact]
    public void IllegalTransitions_ThrowInvalidOperationException()
    {
        // ExecutingBatch -> ConfiguringRule is illegal!
        _stateManager.TransitionTo(ApplicationLifecycleState.ExecutingBatch);

        var action = () => _stateManager.TransitionTo(ApplicationLifecycleState.ConfiguringRule);
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Illegal state transition*");
    }

    [Fact]
    public void ConfiguringRule_CannotJumpDirectlyToExecutingBatch()
    {
        _stateManager.TransitionTo(ApplicationLifecycleState.ConfiguringRule);

        var action = () => _stateManager.TransitionTo(ApplicationLifecycleState.ExecutingBatch);
        action.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region Workflow Rules

    [Fact]
    public void WorkflowRule1_SavingRule_TransitionsToIdleWithoutStartingMonitoring()
    {
        // User is editing rule
        _stateManager.TransitionTo(ApplicationLifecycleState.ConfiguringRule);

        // Save rule
        _stateManager.NotifyRuleSaved();

        // State must be Idle, NOT MonitoringRunning!
        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.Idle);
        _stateManager.CurrentState.Should().NotBe(ApplicationLifecycleState.MonitoringRunning);
    }

    [Fact]
    public void WorkflowRule2_ModifyingRule_TransitionsToConfiguringRule_AndMarksRuleUnvalidated()
    {
        // Given a validated preview state
        _stateManager.TransitionTo(ApplicationLifecycleState.ConfiguringRule);
        _stateManager.TransitionTo(ApplicationLifecycleState.Simulating);
        _stateManager.NotifyRuleSimulated();

        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.PreviewReady);
        _stateManager.IsRuleValidated.Should().BeTrue();

        // Act: modify rule
        _stateManager.NotifyRuleModified("Test Rule");

        // Assert: must reset to ConfiguringRule and IsRuleValidated = false
        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.ConfiguringRule);
        _stateManager.IsRuleValidated.Should().BeFalse();
    }

    [Fact]
    public void WorkflowRule3_PausingMonitoring_PreventsNewJobsWhilePreservingState()
    {
        // Given active monitoring
        _stateManager.TransitionTo(ApplicationLifecycleState.MonitoringRunning);

        // User pauses monitoring
        _stateManager.TransitionTo(ApplicationLifecycleState.MonitoringPaused);
        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.MonitoringPaused);

        // Operations running while paused
        _stateManager.NotifyOperationStarted();
        _stateManager.ActiveOperationCount.Should().Be(1);
        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.ExecutingBatch);

        _stateManager.NotifyOperationCompleted();
        _stateManager.ActiveOperationCount.Should().Be(0);

        // Returns to MonitoringPaused
        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.MonitoringPaused);
    }

    [Fact]
    public void OperationTracking_TracksInFlightCounts_AndTransitionsBackWhenZero()
    {
        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.Idle);

        _stateManager.NotifyOperationStarted();
        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.ExecutingBatch);
        _stateManager.ActiveOperationCount.Should().Be(1);

        _stateManager.NotifyOperationStarted();
        _stateManager.ActiveOperationCount.Should().Be(2);

        _stateManager.NotifyOperationCompleted();
        _stateManager.ActiveOperationCount.Should().Be(1);
        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.ExecutingBatch);

        _stateManager.NotifyOperationCompleted();
        _stateManager.ActiveOperationCount.Should().Be(0);
        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.Idle);
    }

    [Fact]
    public void ModifyingRule_WhileExecutingBatch_ThrowsInvalidOperationException()
    {
        _stateManager.NotifyOperationStarted();
        _stateManager.CurrentState.Should().Be(ApplicationLifecycleState.ExecutingBatch);

        var action = () => _stateManager.NotifyRuleModified("Active Rule");
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot modify rule configuration while operations are executing*");
    }

    #endregion

    #region State Event Notification

    [Fact]
    public void StateChanged_FiresOnEveryValidTransition()
    {
        StateChangedEventArgs? eventArgs = null;
        _stateManager.StateChanged += (s, e) => eventArgs = e;

        _stateManager.TransitionTo(ApplicationLifecycleState.ConfiguringRule, "Edit started");

        eventArgs.Should().NotBeNull();
        eventArgs!.PreviousState.Should().Be(ApplicationLifecycleState.Idle);
        eventArgs.NewState.Should().Be(ApplicationLifecycleState.ConfiguringRule);
        eventArgs.Reason.Should().Be("Edit started");
    }

    #endregion
}

