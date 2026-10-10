using System.Collections.ObjectModel;
using Microsoft.EntityFrameworkCore;
using Moq;
using TidyUp.Data;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services.Rules;
using Xunit;

namespace TidyUp.Tests.Unit.Rules;

public class RuleRevisionManagerTests : IDisposable
{
    private readonly string _dbPath;
    private readonly TidyUpDbContext _context;

    public RuleRevisionManagerTests()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"tidyup_rulerev_{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<TidyUpDbContext>()
            .UseSqlite($"Data Source={_dbPath}")
            .Options;

        _context = new TidyUpDbContext(options);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _context.Dispose();
        try
        {
            if (File.Exists(_dbPath))
            {
                File.Delete(_dbPath);
            }
        }
        catch
        {
            // Ignore temp file cleanup error
        }
    }

    [Fact]
    public async Task SaveRevisionAsync_IncrementsVersionNumberAndStoresSnapshot()
    {
        // Arrange
        var manager = new RuleRevisionManager(_context);
        var ruleId = Guid.NewGuid();
        var rule = new Rule
        {
            Id = ruleId,
            Name = "Initial Rule",
            IsEnabled = true,
            MonitoredFolders = [new MonitoredFolder { Path = @"C:\Folder1" }],
            Conditions = new ConditionGroup { Conditions = [new FileExtensionCondition { Value = "pdf" }] },
            Actions = [new MoveFileAction { DestinationPath = @"C:\Dest1" }]
        };

        // Act 1: Save version 1
        var rev1 = await manager.SaveRevisionAsync(rule, "First version");

        // Assert 1
        Assert.Equal(1, rev1.VersionNumber);
        Assert.Equal("Initial Rule", rev1.RuleName);
        Assert.Equal("First version", rev1.ChangeDescription);
        Assert.False(string.IsNullOrWhiteSpace(rev1.SerializedRuleJson));

        // Act 2: Modify rule and save version 2
        rule.Name = "Updated Rule Name";
        rule.MonitoredFolders.Add(new MonitoredFolder { Path = @"C:\Folder2" });
        var rev2 = await manager.SaveRevisionAsync(rule, "Added Folder2");

        // Assert 2
        Assert.Equal(2, rev2.VersionNumber);
        Assert.Equal("Updated Rule Name", rev2.RuleName);

        var list = await manager.GetRevisionsForRuleAsync(ruleId);
        Assert.Equal(2, list.Count);
        Assert.Equal(2, list[0].VersionNumber);
        Assert.Equal(1, list[1].VersionNumber);
    }

    [Fact]
    public async Task DiffRevisions_AccuratelyDetectsChangesBetweenRevisions()
    {
        // Arrange
        var manager = new RuleRevisionManager(_context);
        var ruleId = Guid.NewGuid();

        var v1Rule = new Rule
        {
            Id = ruleId,
            Name = "V1 Name",
            IsEnabled = true,
            MonitoredFolders = [new MonitoredFolder { Path = @"C:\OldFolder" }],
            Conditions = new ConditionGroup { Conditions = [new FileExtensionCondition { Value = "txt" }] },
            Actions = [new MoveFileAction { DestinationPath = @"C:\OldDest" }]
        };

        var rev1 = await manager.SaveRevisionAsync(v1Rule, "v1");

        var v2Rule = new Rule
        {
            Id = ruleId,
            Name = "V2 Name",
            IsEnabled = false,
            MonitoredFolders = [new MonitoredFolder { Path = @"C:\NewFolder" }],
            Conditions = new ConditionGroup { Conditions = [new FileExtensionCondition { Value = "docx" }] },
            Actions = [new CopyFileAction { DestinationPath = @"C:\NewDest" }]
        };

        var rev2 = await manager.SaveRevisionAsync(v2Rule, "v2");

        // Act
        var diff = manager.DiffRevisions(rev1, rev2);

        // Assert
        Assert.True(diff.HasChanges);
        Assert.True(diff.HasNameChange);
        Assert.Equal("V1 Name", diff.OldName);
        Assert.Equal("V2 Name", diff.NewName);
        Assert.True(diff.HasStatusChange);
        Assert.True(diff.OldIsEnabled);
        Assert.False(diff.NewIsEnabled);

        Assert.Contains(@"C:\NewFolder", diff.AddedFolders);
        Assert.Contains(@"C:\OldFolder", diff.RemovedFolders);

        Assert.True(diff.HasConditionChange);
        Assert.Single(diff.AddedActions);
        Assert.Single(diff.RemovedActions);
    }

    [Fact]
    public async Task RestoreRevisionAsync_ReconstructsRuleAndSavesNewRestorationSnapshot()
    {
        // Arrange
        var mockRepo = new Mock<IRuleRepository>();
        var manager = new RuleRevisionManager(_context, mockRepo.Object);

        var ruleId = Guid.NewGuid();
        var originalRule = new Rule
        {
            Id = ruleId,
            Name = "Original Clean Rule",
            IsEnabled = true,
            MonitoredFolders = [new MonitoredFolder { Path = @"C:\Original" }],
            Conditions = new ConditionGroup { Conditions = [new FileExtensionCondition { Value = "pdf" }] },
            Actions = [new MoveFileAction { DestinationPath = @"C:\OriginalDest" }]
        };

        var rev1 = await manager.SaveRevisionAsync(originalRule, "Initial");

        // Mutate rule to v2
        originalRule.Name = "Corrupted/Bad Edits";
        await manager.SaveRevisionAsync(originalRule, "Bad edit");

        // Act: Restore rev1
        var restored = await manager.RestoreRevisionAsync(rev1.RevisionId);

        // Assert
        Assert.Equal("Original Clean Rule", restored.Name);
        Assert.Single(restored.MonitoredFolders);
        Assert.Equal(@"C:\Original", restored.MonitoredFolders[0].Path);

        // Repository update was invoked
        mockRepo.Verify(r => r.UpdateAsync(It.Is<Rule>(r => r.Name == "Original Clean Rule")), Times.Once);

        // A new revision was added recording the rollback
        var allRevs = await manager.GetRevisionsForRuleAsync(ruleId);
        Assert.Equal(3, allRevs.Count);
        Assert.Equal(3, allRevs[0].VersionNumber);
        Assert.Contains("Restored from Version 1", allRevs[0].ChangeDescription);
    }
}
