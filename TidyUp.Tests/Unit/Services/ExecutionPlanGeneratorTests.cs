using System.Collections.ObjectModel;
using System.Security.Cryptography;
using FluentAssertions;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.Simulation;
using Xunit;

namespace TidyUp.Tests.Unit.Services;

public class ExecutionPlanGeneratorTests : IDisposable
{
    private readonly string _testRoot;
    private readonly IRuleEngine _ruleEngine;
    private readonly IVariableEngine _variableEngine;
    private readonly ExecutionPlanGenerator _generator;

    public ExecutionPlanGeneratorTests()
    {
        _testRoot = Path.Combine(Path.GetTempPath(), "TidyUp_SimulationTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testRoot);

        _variableEngine = new VariableEngine();
        _ruleEngine = new RuleEngine();
        _generator = new ExecutionPlanGenerator(_ruleEngine, _variableEngine);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testRoot))
            {
                Directory.Delete(_testRoot, recursive: true);
            }
        }
        catch
        {
            // Best effort temp cleanup
        }
    }

    private static string ComputeFileSha256(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        return Convert.ToHexString(hash);
    }

    [Fact]
    public async Task GeneratePlanForFolderAsync_WithMoveRule_GeneratesAccuratePlan()
    {
        // Arrange
        var sourceDir = Path.Combine(_testRoot, "Source");
        var targetDir = Path.Combine(_testRoot, "Target");
        Directory.CreateDirectory(sourceDir);

        var doc1 = Path.Combine(sourceDir, "document1.pdf");
        var doc2 = Path.Combine(sourceDir, "image.jpg");
        await File.WriteAllTextAsync(doc1, "PDF content");
        await File.WriteAllTextAsync(doc2, "JPG content");

        var rule = new Rule
        {
            Name = "Move PDFs",
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions = new ObservableCollection<Condition>
                {
                    new FileExtensionCondition
                    {
                        Operator = StringOperator.Is,
                        Value = "pdf"
                    }
                }
            },
            Actions = new ObservableCollection<FileAction>
            {
                new MoveFileAction
                {
                    DestinationPath = targetDir,
                    ConflictResolution = ConflictResolution.Skip,
                    Order = 0
                }
            }
        };

        // Act
        var plan = await _generator.GeneratePlanForFolderAsync(rule, sourceDir, includeSubfolders: false);

        // Assert
        plan.Should().NotBeNull();
        plan.RuleName.Should().Be("Move PDFs");
        plan.TotalScanned.Should().Be(2);
        plan.TotalMatched.Should().Be(1);
        plan.MoveCount.Should().Be(1);
        plan.ConflictCount.Should().Be(0);

        var action = plan.PlannedActions.Should().ContainSingle().Subject;
        action.ActionType.Should().Be(ActionType.Move);
        action.SourcePath.Should().Be(doc1);
        action.TargetPath.Should().Be(Path.Combine(targetDir, "document1.pdf"));
        action.HasConflict.Should().BeFalse();
    }

    [Fact]
    public async Task GeneratePlanForFolderAsync_DetectsDestinationConflict_WhenTargetAlreadyExists()
    {
        // Arrange
        var sourceDir = Path.Combine(_testRoot, "SourceConflict");
        var targetDir = Path.Combine(_testRoot, "TargetConflict");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(targetDir);

        var sourceFile = Path.Combine(sourceDir, "report.pdf");
        var collidingTargetFile = Path.Combine(targetDir, "report.pdf");
        await File.WriteAllTextAsync(sourceFile, "Source report");
        await File.WriteAllTextAsync(collidingTargetFile, "Existing target report");

        var rule = new Rule
        {
            Name = "Detect Conflict Rule",
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions = new ObservableCollection<Condition>
                {
                    new FileExtensionCondition { Operator = StringOperator.Is, Value = "pdf" }
                }
            },
            Actions = new ObservableCollection<FileAction>
            {
                new MoveFileAction
                {
                    DestinationPath = targetDir,
                    ConflictResolution = ConflictResolution.Overwrite,
                    Order = 0
                }
            }
        };

        // Act
        var plan = await _generator.GeneratePlanForFolderAsync(rule, sourceDir, includeSubfolders: false);

        // Assert
        plan.ConflictCount.Should().Be(1);
        plan.HasConflicts.Should().BeTrue();

        var plannedAction = plan.PlannedActions.Should().ContainSingle().Subject;
        plannedAction.HasConflict.Should().BeTrue();
        plannedAction.ConflictDescription.Should().Contain("already exists");
    }

    [Fact]
    public async Task GeneratePlanForFolderAsync_MultiActionChain_ChainsVirtualPathAccurately()
    {
        // Arrange
        var sourceDir = Path.Combine(_testRoot, "ChainedSource");
        var targetDir = Path.Combine(_testRoot, "ChainedTarget");
        Directory.CreateDirectory(sourceDir);

        var file = Path.Combine(sourceDir, "invoice.pdf");
        await File.WriteAllTextAsync(file, "Invoice Content");

        var rule = new Rule
        {
            Name = "Rename and Move Rule",
            IsEnabled = true,
            Conditions = new ConditionGroup { Operator = LogicOperator.And },
            Actions = new ObservableCollection<FileAction>
            {
                new RenameFileAction
                {
                    NamePattern = "Archived_{filename}",
                    Order = 0
                },
                new MoveFileAction
                {
                    DestinationPath = targetDir,
                    Order = 1
                }
            }
        };

        // Act
        var plan = await _generator.GeneratePlanForFolderAsync(rule, sourceDir, includeSubfolders: false);

        // Assert
        plan.PlannedActions.Should().HaveCount(2);

        var firstAction = plan.PlannedActions[0];
        firstAction.ActionType.Should().Be(ActionType.Rename);
        firstAction.TargetPath.Should().Be(Path.Combine(sourceDir, "Archived_invoice.pdf"));

        var secondAction = plan.PlannedActions[1];
        secondAction.ActionType.Should().Be(ActionType.Move);
        secondAction.SourcePath.Should().Be(Path.Combine(sourceDir, "Archived_invoice.pdf"));
        secondAction.TargetPath.Should().Be(Path.Combine(targetDir, "Archived_invoice.pdf"));
    }

    [Fact]
    public async Task GeneratePlanForFolderAsync_DoesNotModifyFilesystem_InvariantProof()
    {
        // Arrange
        var sourceDir = Path.Combine(_testRoot, "ReadOnlyTest");
        var targetDir = Path.Combine(_testRoot, "ReadOnlyTarget");
        Directory.CreateDirectory(sourceDir);

        var testFile = Path.Combine(sourceDir, "critical_user_file.txt");
        await File.WriteAllTextAsync(testFile, "DO NOT MODIFY OR DELETE");

        var originalTimestamp = File.GetLastWriteTimeUtc(testFile);
        var originalHash = ComputeFileSha256(testFile);

        var rule = new Rule
        {
            Name = "Potentially Destructive Rule",
            IsEnabled = true,
            Conditions = new ConditionGroup { Operator = LogicOperator.And },
            Actions = new ObservableCollection<FileAction>
            {
                new MoveFileAction { DestinationPath = targetDir, Order = 0 },
                new DeleteFileAction { Order = 1 }
            }
        };

        // Act
        var plan = await _generator.GeneratePlanForFolderAsync(rule, sourceDir, includeSubfolders: false);

        // Assert - Plan properties
        plan.TotalMatched.Should().Be(1);
        plan.PlannedActions.Should().HaveCount(2);

        // Assert - INVARIANT: Filesystem must be 100% UNCHANGED
        File.Exists(testFile).Should().BeTrue("Original source file must still exist");
        File.GetLastWriteTimeUtc(testFile).Should().Be(originalTimestamp, "File timestamps must be untouched");
        ComputeFileSha256(testFile).Should().Be(originalHash, "File contents and hash must be identical");
        Directory.Exists(targetDir).Should().BeFalse("Simulation must never create destination directories");
    }

    [Fact]
    public async Task GeneratePlanForFolderAsync_RespectsExclusionPatterns()
    {
        // Arrange
        var sourceDir = Path.Combine(_testRoot, "ExclusionSource");
        var gitDir = Path.Combine(sourceDir, ".git");
        Directory.CreateDirectory(gitDir);

        var normalFile = Path.Combine(sourceDir, "data.txt");
        var gitFile = Path.Combine(gitDir, "HEAD");
        await File.WriteAllTextAsync(normalFile, "Regular data");
        await File.WriteAllTextAsync(gitFile, "ref: refs/heads/main");

        var rule = new Rule
        {
            Name = "Exclusion Test Rule",
            IsEnabled = true,
            Conditions = new ConditionGroup { Operator = LogicOperator.And },
            Actions = new ObservableCollection<FileAction>
            {
                new DeleteFileAction { Order = 0 }
            }
        };

        // Act
        var plan = await _generator.GeneratePlanForFolderAsync(
            rule,
            sourceDir,
            includeSubfolders: true,
            exclusionPatterns: new[] { ".git" });

        // Assert
        plan.TotalScanned.Should().Be(2);
        plan.TotalMatched.Should().Be(1);
        plan.PlannedActions.Should().ContainSingle()
            .Which.SourcePath.Should().Be(normalFile);
    }

    [Fact]
    public async Task GeneratePlanForFolderAsync_RespectsCancellationToken()
    {
        // Arrange
        var sourceDir = Path.Combine(_testRoot, "CtsSource");
        Directory.CreateDirectory(sourceDir);

        for (int i = 0; i < 10; i++)
        {
            await File.WriteAllTextAsync(Path.Combine(sourceDir, $"file_{i}.txt"), $"Content {i}");
        }

        var rule = new Rule
        {
            Name = "Cancel Test Rule",
            IsEnabled = true,
            Conditions = new ConditionGroup { Operator = LogicOperator.And },
            Actions = new ObservableCollection<FileAction> { new DeleteFileAction { Order = 0 } }
        };

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        // Act
        var plan = await _generator.GeneratePlanForFolderAsync(
            rule,
            sourceDir,
            includeSubfolders: false,
            cancellationToken: cts.Token);

        // Assert
        plan.TotalMatched.Should().Be(0);
        plan.PlannedActions.Should().BeEmpty();
    }

    [Fact]
    public async Task GeneratePlanForFolderAsync_WhenRuleIsDisabled_SimulatesMatchingFilesCorrectly()
    {
        // Arrange
        var sourceDir = Path.Combine(_testRoot, "DisabledRuleSource");
        var targetDir = Path.Combine(_testRoot, "DisabledRuleTarget");
        Directory.CreateDirectory(sourceDir);

        var doc1 = Path.Combine(sourceDir, "archive.zip");
        var doc2 = Path.Combine(sourceDir, "notes.txt");
        await File.WriteAllTextAsync(doc1, "Fake ZIP");
        await File.WriteAllTextAsync(doc2, "Fake TXT");

        var disabledRule = new Rule
        {
            Name = "Disabled Move Rule",
            IsEnabled = false, // DISABLED RULE!
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileExtensionCondition { Operator = StringOperator.Is, Value = "zip" }
                }
            },
            Actions = new ObservableCollection<FileAction>
            {
                new MoveFileAction { DestinationPath = targetDir, Order = 0 }
            }
        };

        // Act
        var plan = await _generator.GeneratePlanForFolderAsync(
            disabledRule,
            sourceDir,
            includeSubfolders: false);

        // Assert: Disabled rule must successfully simulate matching files in preview
        plan.TotalScanned.Should().Be(2);
        plan.TotalMatched.Should().Be(1);
        plan.PlannedActions.Should().ContainSingle();
        plan.PlannedActions[0].SourcePath.Should().Be(doc1);
        plan.PlannedActions[0].TargetPath.Should().Be(Path.Combine(targetDir, "archive.zip"));

        // Invariant: Rule must still remain disabled
        disabledRule.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task MultiActionChain_RenameThenMove_WiresDependenciesAndPassesPreFlight()
    {
        // Arrange
        var sourceDir = Path.Combine(_testRoot, "ChainSource");
        var destDir = Path.Combine(_testRoot, "ChainDest");
        Directory.CreateDirectory(sourceDir);
        Directory.CreateDirectory(destDir);

        var originalFile = Path.Combine(sourceDir, "report.pdf");
        await File.WriteAllTextAsync(originalFile, "Report content");

        var rule = new Rule
        {
            Name = "Rename Then Move Rule",
            IsEnabled = true,
            Conditions = new ConditionGroup(),
            Actions = new ObservableCollection<FileAction>
            {
                new RenameFileAction { NamePattern = "report_v2", Order = 0 },
                new MoveFileAction { DestinationPath = destDir, Order = 1 }
            }
        };

        // Act
        var plan = await _generator.GeneratePlanForFolderAsync(rule, sourceDir, includeSubfolders: false);

        // Assert
        plan.PlannedActions.Should().HaveCount(2);

        var renameAction = plan.PlannedActions[0];
        var moveAction = plan.PlannedActions[1];

        renameAction.ActionType.Should().Be(ActionType.Rename);
        renameAction.SourcePath.Should().Be(originalFile);
        renameAction.TargetPath.Should().Be(Path.Combine(sourceDir, "report_v2.pdf"));
        renameAction.InitialSourcePath.Should().Be(originalFile);
        renameAction.DependsOnActionId.Should().BeNull();

        moveAction.ActionType.Should().Be(ActionType.Move);
        moveAction.SourcePath.Should().Be(Path.Combine(sourceDir, "report_v2.pdf"));
        moveAction.TargetPath.Should().Be(Path.Combine(destDir, "report_v2.pdf"));
        moveAction.InitialSourcePath.Should().Be(originalFile);
        moveAction.DependsOnActionId.Should().Be(renameAction.Id);

        // PreFlight check validation
        var mockExecutor = new Moq.Mock<TidyUp.Services.IActionExecutor>();
        var previewVm = new TidyUp.ViewModels.PreviewViewModel(_generator, mockExecutor.Object);

        var items = plan.PlannedActions.Select(a => new TidyUp.ViewModels.PlannedActionItemViewModel(a)).ToList();
        var (success, issues) = previewVm.PerformPreFlightCheck(items);

        success.Should().BeTrue("multi-action chain must pass pre-flight without requiring intermediate paths on disk");
        issues.Should().BeEmpty();
    }

    [Fact]
    public async Task MultiActionChain_CopyApplyToSourceFile_ControlsSubsequentActionSource()
    {
        var sourceDir = Path.Combine(_testRoot, "CopySource");
        var copyDestDir = Path.Combine(_testRoot, "CopyDest");
        var moveDestDir = Path.Combine(_testRoot, "MoveDest");
        Directory.CreateDirectory(sourceDir);

        var testFile = Path.Combine(sourceDir, "data.csv");
        await File.WriteAllTextAsync(testFile, "1,2,3");

        // 1. Copy with ApplyToSourceFile = true (subsequent Move moves the original source)
        var ruleOriginal = new Rule
        {
            Name = "Copy Original Rule",
            IsEnabled = true,
            Actions = new ObservableCollection<FileAction>
            {
                new CopyFileAction { DestinationPath = copyDestDir, ApplyToSourceFile = true, Order = 0 },
                new MoveFileAction { DestinationPath = moveDestDir, Order = 1 }
            }
        };

        var plan1 = await _generator.GeneratePlanForFolderAsync(ruleOriginal, sourceDir, includeSubfolders: false);
        plan1.PlannedActions.Should().HaveCount(2);
        plan1.PlannedActions[1].SourcePath.Should().Be(testFile, "when ApplyToSourceFile is true, subsequent action operates on original source");

        // 2. Copy with ApplyToSourceFile = false (subsequent Move moves the copied file)
        var ruleCopy = new Rule
        {
            Name = "Copy Target Rule",
            IsEnabled = true,
            Actions = new ObservableCollection<FileAction>
            {
                new CopyFileAction { DestinationPath = copyDestDir, ApplyToSourceFile = false, Order = 0 },
                new MoveFileAction { DestinationPath = moveDestDir, Order = 1 }
            }
        };

        var plan2 = await _generator.GeneratePlanForFolderAsync(ruleCopy, sourceDir, includeSubfolders: false);
        plan2.PlannedActions.Should().HaveCount(2);
        plan2.PlannedActions[1].SourcePath.Should().Be(Path.Combine(copyDestDir, "data.csv"), "when ApplyToSourceFile is false, subsequent action operates on destination copy");
    }

    [Fact]
    public void PerformPreFlightCheck_WhenPredecessorDeselected_FailsDependencyClosure()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        var action1 = new PlannedFileAction
        {
            Id = id1,
            ActionType = ActionType.Rename,
            SourcePath = @"C:\Test\doc.txt",
            TargetPath = @"C:\Test\doc_renamed.txt",
            InitialSourcePath = @"C:\Test\doc.txt"
        };
        var action2 = new PlannedFileAction
        {
            Id = id2,
            ActionType = ActionType.Move,
            SourcePath = @"C:\Test\doc_renamed.txt",
            TargetPath = @"C:\Dest\doc_renamed.txt",
            InitialSourcePath = @"C:\Test\doc.txt",
            DependsOnActionId = id1
        };

        var item2 = new TidyUp.ViewModels.PlannedActionItemViewModel(action2) { IsApproved = true };

        var mockExecutor = new Moq.Mock<TidyUp.Services.IActionExecutor>();
        var previewVm = new TidyUp.ViewModels.PreviewViewModel(_generator, mockExecutor.Object);

        // PreFlight only receives item2 (item1 was deselected from batch)
        var (success, issues) = previewVm.PerformPreFlightCheck(new[] { item2 });

        success.Should().BeFalse("cannot execute dependent action without approved predecessor");
        issues.Should().ContainSingle().Which.Should().Contain("depends on an unselected preceding action");
    }
}

