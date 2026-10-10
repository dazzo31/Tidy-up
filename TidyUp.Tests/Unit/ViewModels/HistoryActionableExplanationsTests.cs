using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TidyUp.Data;
using TidyUp.Data.Entities;
using TidyUp.Services.FileSystem;
using TidyUp.Services.Rollback;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

public class HistoryActionableExplanationsTests : IDisposable
{
    private readonly string _tempTestDir;
    private readonly TidyUpDbContext _dbContext;
    private readonly Mock<IRollbackEngine> _rollbackEngineMock;

    public HistoryActionableExplanationsTests()
    {
        _tempTestDir = Path.Combine(Path.GetTempPath(), $"TidyUp_HistoryTest_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempTestDir);

        var dbPath = Path.Combine(_tempTestDir, "test_history.db");
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<TidyUp.Data.TidyUpDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        _dbContext = new TidyUp.Data.TidyUpDbContext(options);
        _dbContext.Database.EnsureCreated();

        _rollbackEngineMock = new Mock<IRollbackEngine>();
    }

    public void Dispose()
    {
        try
        {
            _dbContext.Dispose();
            if (Directory.Exists(_tempTestDir))
            {
                Directory.Delete(_tempTestDir, true);
            }
        }
        catch { }
    }

    [Theory]
    [InlineData("System.UnauthorizedAccessException: Access to the path 'C:\\dest' is denied.", "Permission Denied")]
    [InlineData("The process cannot access the file because it is being used by another process.", "File In Use")]
    [InlineData("System.IO.DirectoryNotFoundException: Could not find a part of the path.", "Folder Not Found")]
    [InlineData("System.IO.PathTooLongException: The specified path, file name, or both are too long.", "Path Too Long")]
    [InlineData("There is not enough space on the disk.", "Disk Full")]
    [InlineData("Target file 'C:\\dest.txt' was modified by an external process after operation.", "Safety Lock")]
    [InlineData("Original path 'C:\\dest.txt' is occupied by another file.", "Destination Conflict")]
    public void ExplainError_MapsTechnicalExceptionsToPlainLanguageExplanations(string rawError, string expectedPrefix)
    {
        var explanation = ActionableErrorExplainer.ExplainError(rawError);

        explanation.Should().StartWith(expectedPrefix);
    }

    [Fact]
    public void HistoryRowItemViewModel_WhenFailed_ExposesHasErrorAndExplanation()
    {
        var entry = new OperationJournalEntry
        {
            OperationId = Guid.NewGuid(),
            BatchId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            ActionType = "Move",
            OriginalPath = "C:\\src\\data.txt",
            TargetPath = "C:\\dst\\data.txt",
            Status = "Failed",
            Details = "System.UnauthorizedAccessException: Access is denied."
        };

        var rowVm = new HistoryRowItemViewModel(entry);

        rowVm.HasError.Should().BeTrue();
        rowVm.ActionableExplanation.Should().Contain("Permission Denied");
        rowVm.CanRollback.Should().BeFalse();
    }

    [Fact]
    public void HistoryRowItemViewModel_ToggleDiagnostics_TogglesExpandedState()
    {
        var entry = new OperationJournalEntry
        {
            OperationId = Guid.NewGuid(),
            Status = "Completed"
        };

        var rowVm = new HistoryRowItemViewModel(entry);
        rowVm.IsDiagnosticsExpanded.Should().BeFalse();

        rowVm.ToggleDiagnostics();
        rowVm.IsDiagnosticsExpanded.Should().BeTrue();

        rowVm.ToggleDiagnostics();
        rowVm.IsDiagnosticsExpanded.Should().BeFalse();
    }

    [Fact]
    public async Task RollbackOperationAsync_WhenTargetModifiedAfterOperation_BlocksReversalWithSafetyExplanation()
    {
        var targetFile = Path.Combine(_tempTestDir, "moved.txt");
        File.WriteAllText(targetFile, "original post-action content");
        var originalHash = FileChecksumHelper.ComputeSha256(targetFile);

        // Simulate external modification to target file
        File.WriteAllText(targetFile, "tampered / modified by external editor");

        var entry = new OperationJournalEntry
        {
            OperationId = Guid.NewGuid(),
            BatchId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            ActionType = "Move",
            OriginalPath = Path.Combine(_tempTestDir, "source.txt"),
            TargetPath = targetFile,
            PostActionHash = originalHash,
            Status = "Completed"
        };

        var rowVm = new HistoryRowItemViewModel(entry);

        var historyVm = new HistoryViewModel(
            _rollbackEngineMock.Object,
            directContext: _dbContext)
        {
            ConfirmRollbackHandler = (_, _) => true
        };

        await historyVm.RollbackOperationAsync(rowVm);

        rowVm.RollbackMessage.Should().Contain("Safety Lock");
        rowVm.RollbackMessage.Should().Contain("modified externally");
        historyVm.StatusMessage.Should().Contain("Rollback aborted");

        // Verify rollback engine was NOT called because pre-rollback check protected the user
        _rollbackEngineMock.Verify(r => r.RollbackOperationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
