using System.IO;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using TidyUp.Data;
using TidyUp.Data.Entities;
using TidyUp.Services.Rollback;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

public class HistoryViewModelTests : IDisposable
{
    private readonly string _testTempDir;
    private readonly TidyUpDbContext _dbContext;
    private readonly Mock<IRollbackEngine> _rollbackEngineMock;
    private readonly HistoryViewModel _viewModel;

    public HistoryViewModelTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "TidyUp_HistoryVmTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);

        var dbPath = Path.Combine(_testTempDir, "test_history.db");
        var options = new DbContextOptionsBuilder<TidyUpDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        _dbContext = new TidyUpDbContext(options);
        _dbContext.Database.EnsureCreated();

        _rollbackEngineMock = new Mock<IRollbackEngine>();

        _viewModel = new HistoryViewModel(
            _rollbackEngineMock.Object,
            directContext: _dbContext)
        {
            ConfirmRollbackHandler = (_, _) => true
        };
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        try
        {
            if (Directory.Exists(_testTempDir))
            {
                Directory.Delete(_testTempDir, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    private async Task SeedJournalEntriesAsync(int count, string ruleName = "Test Rule", string actionType = "Move")
    {
        var batchId = Guid.NewGuid();
        for (int i = 0; i < count; i++)
        {
            _dbContext.OperationJournal.Add(new OperationJournalEntry
            {
                OperationId = Guid.NewGuid(),
                BatchId = batchId,
                Timestamp = DateTime.UtcNow.AddMinutes(-i),
                ActionType = actionType,
                RuleName = ruleName,
                OriginalPath = $@"C:\Source\doc_{i}.pdf",
                TargetPath = $@"C:\Target\doc_{i}.pdf",
                PreActionHash = "AABBCCDD11223344",
                PostActionHash = "AABBCCDD11223344",
                Status = "Completed",
                Details = $"Executed {actionType} for item {i}"
            });
        }
        await _dbContext.SaveChangesAsync();
    }

    #region Pagination Tests

    [Fact]
    public async Task LoadPageAsync_LoadsFirstPageAndComputesPaginationCounts()
    {
        // Arrange: 30 entries with page size 25
        await SeedJournalEntriesAsync(30);
        _viewModel.PageSize = 25;

        // Act
        await _viewModel.LoadPageAsync();

        // Assert
        _viewModel.TotalRecords.Should().Be(30);
        _viewModel.TotalPages.Should().Be(2);
        _viewModel.CurrentPage.Should().Be(1);
        _viewModel.HistoryEntries.Should().HaveCount(25);
        _viewModel.HasPreviousPage.Should().BeFalse();
        _viewModel.HasNextPage.Should().BeTrue();
        _viewModel.PageSummary.Should().Contain("Page 1 of 2 (30 total entries)");
    }

    [Fact]
    public async Task PaginationCommands_NavigateBetweenPages()
    {
        // Arrange: 30 entries
        await SeedJournalEntriesAsync(30);
        _viewModel.PageSize = 25;
        await _viewModel.LoadPageAsync();

        // Act 1: Next page
        await _viewModel.NextPageAsync();

        // Assert 1: Page 2
        _viewModel.CurrentPage.Should().Be(2);
        _viewModel.HistoryEntries.Should().HaveCount(5);
        _viewModel.HasNextPage.Should().BeFalse();
        _viewModel.HasPreviousPage.Should().BeTrue();

        // Act 2: Previous page
        await _viewModel.PreviousPageAsync();

        // Assert 2: Page 1
        _viewModel.CurrentPage.Should().Be(1);
        _viewModel.HistoryEntries.Should().HaveCount(25);

        // Act 3: Last page
        await _viewModel.LastPageAsync();
        _viewModel.CurrentPage.Should().Be(2);

        // Act 4: First page
        await _viewModel.FirstPageAsync();
        _viewModel.CurrentPage.Should().Be(1);
    }

    #endregion

    #region Filtering Tests

    [Fact]
    public async Task Filter_ByActionType_FiltersRecordsAccurately()
    {
        // Arrange: 5 Move entries, 3 Delete entries
        await SeedJournalEntriesAsync(5, "Move Rule", "Move");
        await SeedJournalEntriesAsync(3, "Delete Rule", "Delete");

        _viewModel.SelectedActionTypeFilter = "Delete";

        // Act
        await _viewModel.LoadPageAsync();

        // Assert
        _viewModel.TotalRecords.Should().Be(3);
        _viewModel.HistoryEntries.Should().HaveCount(3);
        _viewModel.HistoryEntries.Should().OnlyContain(e => e.ActionType == "Delete");
    }

    [Fact]
    public async Task Filter_BySearchText_SearchesOriginalAndTargetPath()
    {
        // Arrange
        _dbContext.OperationJournal.Add(new OperationJournalEntry
        {
            ActionType = "Move",
            OriginalPath = @"C:\Invoices\invoice_2026.pdf",
            TargetPath = @"C:\Archive\invoice_2026.pdf",
            Status = "Completed"
        });
        _dbContext.OperationJournal.Add(new OperationJournalEntry
        {
            ActionType = "Move",
            OriginalPath = @"C:\Photos\photo_1.jpg",
            TargetPath = @"C:\Archive\photo_1.jpg",
            Status = "Completed"
        });
        await _dbContext.SaveChangesAsync();

        _viewModel.SearchText = "invoice";

        // Act
        await _viewModel.LoadPageAsync();

        // Assert
        _viewModel.TotalRecords.Should().Be(1);
        _viewModel.HistoryEntries.Should().HaveCount(1);
        _viewModel.HistoryEntries[0].OriginalPath.Should().Contain("invoice_2026.pdf");
    }

    #endregion

    #region Rollback Tests

    [Fact]
    public async Task RollbackOperationAsync_CancelledByUser_DoesNotCallRollbackEngine()
    {
        // Arrange
        await SeedJournalEntriesAsync(1);
        await _viewModel.LoadPageAsync();
        var item = _viewModel.HistoryEntries[0];

        _viewModel.ConfirmRollbackHandler = (_, _) => false; // User cancels

        // Act
        await _viewModel.RollbackOperationAsync(item);

        // Assert
        _rollbackEngineMock.Verify(
            x => x.RollbackOperationAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        item.Status.Should().Be("Completed");
    }

    [Fact]
    public async Task RollbackOperationAsync_ConfirmedByUser_CallsRollbackEngineAndUpdatesStatus()
    {
        // Arrange
        await SeedJournalEntriesAsync(1);
        await _viewModel.LoadPageAsync();
        var item = _viewModel.HistoryEntries[0];

        _rollbackEngineMock
            .Setup(x => x.RollbackOperationAsync(item.OperationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RollbackOperationResult
            {
                OperationId = item.OperationId,
                Success = true,
                Message = "File successfully returned to original location."
            });

        _viewModel.ConfirmRollbackHandler = (_, _) => true;

        // Act
        await _viewModel.RollbackOperationAsync(item);

        // Assert
        _rollbackEngineMock.Verify(
            x => x.RollbackOperationAsync(item.OperationId, It.IsAny<CancellationToken>()),
            Times.Once);

        item.Status.Should().Be("RolledBack");
        item.CanRollback.Should().BeFalse();
        item.RolledBackAt.Should().NotBeNull();
        item.RollbackMessage.Should().Contain("successfully reversed");
    }

    [Fact]
    public async Task RollbackBatchAsync_ConfirmedByUser_ExecutesBatchRollback()
    {
        // Arrange
        await SeedJournalEntriesAsync(3);
        await _viewModel.LoadPageAsync();
        var item = _viewModel.HistoryEntries[0];

        _rollbackEngineMock
            .Setup(x => x.RollbackBatchAsync(item.BatchId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RollbackBatchResult
            {
                BatchId = item.BatchId,
                OverallSuccess = true,
                TotalOperations = 3,
                RolledBackCount = 3,
                FailedCount = 0
            });

        _viewModel.ConfirmRollbackHandler = (_, _) => true;

        // Act
        await _viewModel.RollbackBatchAsync(item);

        // Assert
        _rollbackEngineMock.Verify(
            x => x.RollbackBatchAsync(item.BatchId, It.IsAny<CancellationToken>()),
            Times.Once);
        _viewModel.StatusMessage.Should().Contain("3 reversed");
    }

    #endregion

    #region Streaming Export Tests

    [Fact]
    public async Task ExportToCsvAsync_StreamsChunksToCsvFileWithoutFullMemoryLoad()
    {
        // Arrange: 10 records
        await SeedJournalEntriesAsync(10);
        var exportPath = Path.Combine(_testTempDir, "export.csv");

        // Act
        await _viewModel.ExportToCsvAsync(exportPath);

        // Assert
        File.Exists(exportPath).Should().BeTrue();
        var lines = await File.ReadAllLinesAsync(exportPath);
        lines.Length.Should().Be(11); // 1 header line + 10 data lines
        lines[0].Should().StartWith("OperationId,BatchId,Timestamp,ActionType");
        lines[1].Should().Contain("Move");
    }

    [Fact]
    public async Task ExportToJsonAsync_StreamsChunksToJsonFile()
    {
        // Arrange: 5 records
        await SeedJournalEntriesAsync(5);
        var exportPath = Path.Combine(_testTempDir, "export.json");

        // Act
        await _viewModel.ExportToJsonAsync(exportPath);

        // Assert
        File.Exists(exportPath).Should().BeTrue();
        var content = await File.ReadAllTextAsync(exportPath);
        content.Should().Contain("operationId");
        content.Should().Contain("originalPath");
    }

    #endregion
}

