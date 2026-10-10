using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using System.IO;
using TidyUp.Data;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.FileSystem;
using TidyUp.Services.Rollback;
using TidyUp.Services.Simulation;

namespace TidyUp.Tests.Integration;

public class EndToEndFileActionTests : IDisposable
{
    private readonly TestDirectoryFixture _fixture;
    private readonly TidyUpDbContext _dbContext;
    private readonly RollbackEngine _rollbackEngine;
    private readonly ActionExecutor _actionExecutor;
    private readonly VariableEngine _variableEngine;
    private readonly ExecutionPlanGenerator _simulationEngine;

    public EndToEndFileActionTests()
    {
        _fixture = new TestDirectoryFixture();

        var dbPath = Path.Combine(_fixture.RootDirectory, "integration_journal.db");
        var options = new DbContextOptionsBuilder<TidyUpDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        _dbContext = new TidyUpDbContext(options);
        _dbContext.Database.EnsureCreated();

        _rollbackEngine = new RollbackEngine(directContext: _dbContext);
        _variableEngine = new VariableEngine();
        _actionExecutor = new ActionExecutor(
            _variableEngine,
            fileLockDetector: new FileLockDetector(),
            rollbackEngine: _rollbackEngine);
        _simulationEngine = new ExecutionPlanGenerator(new RuleEngine(), _variableEngine);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        _fixture.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Simulation Consistency

    [Fact]
    public async Task SimulationPlan_MatchesActualExecution_OnRealDisk()
    {
        // Arrange
        var testFile = _fixture.CreateFile("Source/quarterly_report.pdf", "Quarterly Report Content 2026");
        var rule = new Rule
        {
            Name = "Organize Reports",
            Conditions = new ConditionGroup
            {
                Conditions = new System.Collections.ObjectModel.ObservableCollection<Condition>
                {
                    new FileExtensionCondition
                    {
                        Operator = StringOperator.Is,
                        Value = "pdf"
                    }
                }
            },
            Actions = new System.Collections.ObjectModel.ObservableCollection<FileAction>
            {
                new MoveFileAction
                {
                    Order = 1,
                    DestinationPath = _fixture.DestinationDirectory,
                    ConflictResolution = ConflictResolution.Overwrite
                }
            }
        };

        // 1. Simulation Phase (Dry-Run)
        var plan = await _simulationEngine.GeneratePlanForFolderAsync(rule, _fixture.SourceDirectory, includeSubfolders: false);

        plan.RuleName.Should().Be("Organize Reports");
        plan.PlannedActions.Should().HaveCount(1);
        plan.PlannedActions[0].TargetPath.Should().Be(Path.Combine(_fixture.DestinationDirectory, "quarterly_report.pdf"));

        // Verify disk untouched during simulation
        _fixture.FileExists("Source/quarterly_report.pdf").Should().BeTrue();
        _fixture.FileExists("Destination/quarterly_report.pdf").Should().BeFalse();

        // 2. Real Execution Phase
        var results = await _actionExecutor.ExecuteActionsAsync(rule.Actions.ToList(), testFile);

        results.Should().HaveCount(1);
        results.All(r => r.Success).Should().BeTrue();

        // Verify on-disk results match simulation plan exactly
        _fixture.FileExists("Source/quarterly_report.pdf").Should().BeFalse();
        _fixture.FileExists("Destination/quarterly_report.pdf").Should().BeTrue();
        File.ReadAllText(_fixture.GetFullPath("Destination/quarterly_report.pdf"))
            .Should().Be("Quarterly Report Content 2026");
    }

    #endregion

    #region Multi-Action Chains

    [Fact]
    public async Task MultiActionChain_RenameThenMove_ExecutesSuccessfully()
    {
        // Arrange: Chain 1: Rename in-place then Move to destination directory
        var file = _fixture.CreateFile("Source/invoice_draft.txt", "Invoice Draft Total: $500");
        var actions = new List<FileAction>
        {
            new RenameFileAction
            {
                Order = 1,
                NamePattern = "invoice_2026_approved"
            },
            new MoveFileAction
            {
                Order = 2,
                DestinationPath = _fixture.DestinationDirectory,
                ConflictResolution = ConflictResolution.Overwrite
            }
        };

        // Act
        var results = await _actionExecutor.ExecuteActionsAsync(actions, file);

        // Assert
        results.Should().HaveCount(2);
        results.All(r => r.Success).Should().BeTrue();

        _fixture.FileExists("Source/invoice_draft.txt").Should().BeFalse();
        _fixture.FileExists("Source/invoice_2026_approved.txt").Should().BeFalse();
        _fixture.FileExists("Destination/invoice_2026_approved.txt").Should().BeTrue();
        File.ReadAllText(_fixture.GetFullPath("Destination/invoice_2026_approved.txt"))
            .Should().Be("Invoice Draft Total: $500");
    }

    [Fact]
    public async Task MultiActionChain_CopyThenDelete_ExecutesSafely()
    {
        // Arrange: Chain 2: Copy to Archive, then Delete original from Source
        var file = _fixture.CreateFile("Source/project_backup.zip", "PK_ZIP_TEST_DATA");
        var originalChecksum = _fixture.ComputeChecksum("Source/project_backup.zip");

        var actions = new List<FileAction>
        {
            new CopyFileAction
            {
                Order = 1,
                DestinationPath = _fixture.ArchiveDirectory,
                ConflictResolution = ConflictResolution.Overwrite,
                ApplyToSourceFile = true
            },
            new DeleteFileAction
            {
                Order = 2,
                UseRecycleBin = true
            }
        };

        // Act
        var results = await _actionExecutor.ExecuteActionsAsync(actions, file);

        // Assert
        results.Should().HaveCount(2);
        results.All(r => r.Success).Should().BeTrue();

        _fixture.FileExists("Source/project_backup.zip").Should().BeFalse("source file was deleted by 2nd action");
        _fixture.FileExists("Archive/project_backup.zip").Should().BeTrue("file was copied to archive by 1st action");
        _fixture.ComputeChecksum("Archive/project_backup.zip").Should().Be(originalChecksum);
    }

    [Fact]
    public async Task MultiActionChain_DeepFolderHierarchy_CreatesSubfoldersAutomatically()
    {
        // Arrange: Nested source moved to deep destination hierarchy
        var file = _fixture.CreateFile("Source/nested/level1/level2/deep_item.dat", "Deep hierarchy payload");
        var targetDeepDir = Path.Combine(_fixture.DestinationDirectory, "archive", "year_2026", "q1", "finance");

        var moveAction = new MoveFileAction
        {
            Order = 1,
            DestinationPath = targetDeepDir,
            ConflictResolution = ConflictResolution.Overwrite
        };

        // Act
        var result = await _actionExecutor.ExecuteActionAsync(moveAction, file);

        // Assert
        result.Success.Should().BeTrue();
        _fixture.FileExists("Source/nested/level1/level2/deep_item.dat").Should().BeFalse();
        File.Exists(Path.Combine(targetDeepDir, "deep_item.dat")).Should().BeTrue();
    }

    #endregion

    #region Realistic File Permutations

    [Fact]
    public async Task RealisticFiles_ZeroByteFile_ProcessesAndRollsBackCleanly()
    {
        // Arrange: 0-byte file
        var file = _fixture.CreateZeroByteFile("Source/empty_marker.dat");
        var batchId = Guid.NewGuid();

        var actions = new List<FileAction>
        {
            new RenameFileAction
            {
                Order = 1,
                NamePattern = "empty_marker_verified"
            },
            new MoveFileAction
            {
                Order = 2,
                DestinationPath = _fixture.DestinationDirectory,
                ConflictResolution = ConflictResolution.Overwrite
            }
        };

        // Act: Execute
        var results = await _actionExecutor.ExecuteActionsAsync(actions, file, batchId: batchId);
        results.All(r => r.Success).Should().BeTrue();
        _fixture.FileExists("Destination/empty_marker_verified.dat").Should().BeTrue();

        // Act: Rollback
        var rollback = await _rollbackEngine.RollbackBatchAsync(batchId);
        rollback.OverallSuccess.Should().BeTrue();

        // Assert: Restored to original empty file
        _fixture.FileExists("Source/empty_marker.dat").Should().BeTrue();
        _fixture.FileExists("Destination/empty_marker_verified.dat").Should().BeFalse();
        new FileInfo(_fixture.GetFullPath("Source/empty_marker.dat")).Length.Should().Be(0);
    }

    [Fact]
    public async Task RealisticFiles_MultiMegabyteFile_CalculatesChecksumAndTransfersWithoutCorruption()
    {
        // Arrange: 4 MB binary file
        var file = _fixture.CreateLargeFile("Source/large_payload.bin", 4);
        var originalChecksum = _fixture.ComputeChecksum(file.FullName);
        var batchId = Guid.NewGuid();

        var moveAction = new MoveFileAction
        {
            Order = 1,
            DestinationPath = _fixture.DestinationDirectory,
            ConflictResolution = ConflictResolution.Overwrite
        };

        // Act: Execute Move
        var result = await _actionExecutor.ExecuteActionAsync(moveAction, file, batchId: batchId);
        result.Success.Should().BeTrue();

        var destPath = Path.Combine(_fixture.DestinationDirectory, "large_payload.bin");
        _fixture.ComputeChecksum(destPath).Should().Be(originalChecksum);

        // Act: Rollback Move
        var rollback = await _rollbackEngine.RollbackBatchAsync(batchId);
        rollback.OverallSuccess.Should().BeTrue();

        _fixture.FileExists("Source/large_payload.bin").Should().BeTrue();
        _fixture.ComputeChecksum(file.FullName).Should().Be(originalChecksum);
    }

    [Fact]
    public async Task RealisticFiles_UnicodeFilenames_PreservesEncodingAndExecutesCleanly()
    {
        // Arrange: Complex Unicode filename
        const string unicodeName = "財務報告_2026_日本語_🎉.pdf";
        var file = _fixture.CreateUnicodeFile(unicodeName, "Unicode Financial Report Content");
        var originalChecksum = _fixture.ComputeChecksum(file.FullName);
        var batchId = Guid.NewGuid();

        var moveAction = new MoveFileAction
        {
            Order = 1,
            DestinationPath = _fixture.DestinationDirectory,
            ConflictResolution = ConflictResolution.Overwrite
        };

        // Act: Move Unicode file
        var result = await _actionExecutor.ExecuteActionAsync(moveAction, file, batchId: batchId);
        result.Success.Should().BeTrue();

        var destPath = Path.Combine(_fixture.DestinationDirectory, unicodeName);
        File.Exists(destPath).Should().BeTrue();
        _fixture.ComputeChecksum(destPath).Should().Be(originalChecksum);

        // Act: Rollback
        var rollback = await _rollbackEngine.RollbackBatchAsync(batchId);
        rollback.OverallSuccess.Should().BeTrue();

        File.Exists(file.FullName).Should().BeTrue();
        _fixture.ComputeChecksum(file.FullName).Should().Be(originalChecksum);
    }

    [Fact]
    public async Task RealisticFiles_ReadOnlyAttribute_HandledSafely()
    {
        // Arrange: Read-only file
        var file = _fixture.CreateReadOnlyFile("Source/locked_policy.txt", "Strict Read Only Policy");
        var batchId = Guid.NewGuid();

        var moveAction = new MoveFileAction
        {
            Order = 1,
            DestinationPath = _fixture.DestinationDirectory,
            ConflictResolution = ConflictResolution.Overwrite
        };

        // Act
        var result = await _actionExecutor.ExecuteActionAsync(moveAction, file, batchId: batchId);
        result.Success.Should().BeTrue();

        var destFile = Path.Combine(_fixture.DestinationDirectory, "locked_policy.txt");
        File.Exists(destFile).Should().BeTrue();

        // Rollback
        var rollback = await _rollbackEngine.RollbackBatchAsync(batchId);
        rollback.OverallSuccess.Should().BeTrue();

        File.Exists(file.FullName).Should().BeTrue();
    }

    #endregion

    #region Mid-Chain Failure & Rollback

    [Fact]
    public async Task MidChainFailure_RollbackReversesCompletedActions_RestoringInitialState()
    {
        // Arrange: Chain with 2 actions:
        // Action 1: Rename "initial_doc.txt" to "renamed_doc.txt" in Source
        // Action 2: Move to Destination with ConflictResolution.Skip, but Destination already has "renamed_doc.txt"!
        var file = _fixture.CreateFile("Source/initial_doc.txt", "Original Initial Content");
        var originalChecksum = _fixture.ComputeChecksum("Source/initial_doc.txt");

        // Pre-create conflicting file at destination to cause Action 2 to fail on Skip
        _fixture.CreateFile("Destination/renamed_doc.txt", "Conflicting Existing Content");

        var batchId = Guid.NewGuid();
        var actions = new List<FileAction>
        {
            new RenameFileAction
            {
                Order = 1,
                NamePattern = "renamed_doc"
            },
            new MoveFileAction
            {
                Order = 2,
                DestinationPath = _fixture.DestinationDirectory,
                ConflictResolution = ConflictResolution.Skip // Will skip and fail because destination exists
            }
        };

        // Act 1: Execute actions
        var results = await _actionExecutor.ExecuteActionsAsync(actions, file, batchId: batchId);

        // Assert 1: Action 1 succeeded, Action 2 skipped/failed
        results.Should().HaveCount(2);
        results[0].Success.Should().BeTrue("Action 1 (Rename) succeeded");
        results[1].Success.Should().BeFalse("Action 2 (Move) failed due to Skip conflict");

        // Verify intermediate disk state: file was renamed to renamed_doc.txt in Source
        _fixture.FileExists("Source/renamed_doc.txt").Should().BeTrue();
        _fixture.FileExists("Source/initial_doc.txt").Should().BeFalse();

        // Act 2: Roll back the partial batch
        var rollbackResult = await _rollbackEngine.RollbackBatchAsync(batchId);

        // Assert 2: Rollback reversed Action 1
        rollbackResult.OverallSuccess.Should().BeTrue();
        rollbackResult.RolledBackCount.Should().Be(1);

        // Verify final disk state: restored completely back to initial_doc.txt!
        _fixture.FileExists("Source/initial_doc.txt").Should().BeTrue("file must be restored to original name");
        _fixture.FileExists("Source/renamed_doc.txt").Should().BeFalse("renamed intermediate file must be gone");
        _fixture.ComputeChecksum("Source/initial_doc.txt").Should().Be(originalChecksum);
        File.ReadAllText(_fixture.GetFullPath("Source/initial_doc.txt")).Should().Be("Original Initial Content");

        // Verify conflicting destination file was not damaged
        File.ReadAllText(_fixture.GetFullPath("Destination/renamed_doc.txt")).Should().Be("Conflicting Existing Content");
    }

    [Fact]
    public async Task Rollback_AbortsWhenTargetModifiedExternally_ToProtectUserData()
    {
        // Arrange
        var file = _fixture.CreateFile("Source/critical_notes.txt", "Original Critical Notes");
        var batchId = Guid.NewGuid();

        var moveAction = new MoveFileAction
        {
            Order = 1,
            DestinationPath = _fixture.DestinationDirectory,
            ConflictResolution = ConflictResolution.Overwrite
        };

        // Act 1: Execute Move
        var result = await _actionExecutor.ExecuteActionAsync(moveAction, file, batchId: batchId);
        result.Success.Should().BeTrue();

        var targetPath = _fixture.GetFullPath("Destination/critical_notes.txt");
        File.Exists(targetPath).Should().BeTrue();

        // Simulate external user editing file at destination
        File.AppendAllText(targetPath, "\n[NEW EDIT BY USER AT 23:00]");

        // Act 2: Attempt Rollback
        var rollback = await _rollbackEngine.RollbackBatchAsync(batchId);

        // Assert: Rollback rejected to prevent overwriting user modifications
        rollback.OverallSuccess.Should().BeFalse();
        rollback.FailedCount.Should().Be(1);
        rollback.OperationResults.Should().ContainSingle(r =>
            r.ConflictReason == RollbackConflictReason.TargetModifiedByExternalProcess);

        // Target file remains untouched with user changes
        File.ReadAllText(targetPath).Should().Contain("[NEW EDIT BY USER AT 23:00]");
    }

    #endregion

    #region Fixture Cleanup Guarantee

    [Fact]
    public void Fixture_Dispose_CleansUpAllFilesAndDirectories100Percent()
    {
        // Arrange
        string rootPath;
        using (var tempFixture = new TestDirectoryFixture())
        {
            rootPath = tempFixture.RootDirectory;
            tempFixture.CreateFile("Source/file1.txt", "data 1");
            tempFixture.CreateZeroByteFile("Source/empty.dat");
            tempFixture.CreateReadOnlyFile("Source/readonly.log", "readonly data");
            tempFixture.CreateLargeFile("Source/large.bin", 1);
            tempFixture.CreateUnicodeFile("日本語_🎉.txt", "unicode data");
            tempFixture.CreateDirectory("Deep/Nested/Dir/Tree");

            Directory.Exists(rootPath).Should().BeTrue();
        }

        // Assert: After Dispose, root directory and everything in it must be 100% removed
        Directory.Exists(rootPath).Should().BeFalse("fixture must guarantee 100% cleanup of all temp disk resources");
    }

    #endregion
}
