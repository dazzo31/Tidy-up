using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services.Rules;
using Xunit;

namespace TidyUp.Tests.Unit.Rules;

public class MultiRuleConflictAnalyzerTests
{
    private readonly MultiRuleConflictAnalyzer _analyzer = new();

    [Fact]
    public void AnalyzeRules_WhenCompetingDestinationsExist_DetectsConflict()
    {
        // Arrange: Two rules monitoring C:\Downloads matching .pdf with different destinations
        var rule1 = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "PDFs to Folder A",
            IsEnabled = true,
            ExecutionOrder = 1,
            MonitoredFolders = [new MonitoredFolder { Path = @"C:\Downloads" }],
            Conditions = new ConditionGroup
            {
                Conditions = [new FileExtensionCondition { Value = "pdf" }]
            },
            Actions = [new MoveFileAction { DestinationPath = @"C:\FolderA" }]
        };

        var rule2 = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "PDFs to Folder B",
            IsEnabled = true,
            ExecutionOrder = 2,
            MonitoredFolders = [new MonitoredFolder { Path = @"C:\Downloads" }],
            Conditions = new ConditionGroup
            {
                Conditions = [new FileExtensionCondition { Value = "pdf" }]
            },
            Actions = [new MoveFileAction { DestinationPath = @"C:\FolderB" }]
        };

        // Act
        var report = _analyzer.AnalyzeRules([rule1, rule2]);

        // Assert
        Assert.True(report.HasConflicts);
        Assert.Equal(1, report.CompetingDestinationCount);
        var conflict = report.Conflicts.First(c => c.ConflictType == RuleConflictType.CompetingDestinations);
        Assert.Equal(rule1.Id, conflict.PrimaryRuleId);
        Assert.Equal(rule2.Id, conflict.SecondaryRuleId);
        Assert.Contains("FolderA", conflict.Description);
        Assert.Contains("FolderB", conflict.Description);
    }

    [Fact]
    public void AnalyzeRules_WhenPriorRuleConsumesFilesAndSubsumesFilter_DetectsShadowedRule()
    {
        // Arrange: Rule 1 matches all files (no condition) and moves them with StopProcessingAfterMatch = true.
        // Rule 2 matches .pdf files in the same folder.
        var rule1 = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Catch All Rule",
            IsEnabled = true,
            ExecutionOrder = 1,
            StopProcessingAfterMatch = true,
            MonitoredFolders = [new MonitoredFolder { Path = @"C:\Input" }],
            Actions = [new MoveFileAction { DestinationPath = @"C:\Archive" }]
        };

        var rule2 = new Rule
        {
            Id = Guid.NewGuid(),
            Name = "Specific PDF Rule",
            IsEnabled = true,
            ExecutionOrder = 2,
            MonitoredFolders = [new MonitoredFolder { Path = @"C:\Input" }],
            Conditions = new ConditionGroup
            {
                Conditions = [new FileExtensionCondition { Value = "pdf" }]
            },
            Actions = [new MoveFileAction { DestinationPath = @"C:\PDFs" }]
        };

        // Act
        var report = _analyzer.AnalyzeRules([rule1, rule2]);

        // Assert
        Assert.True(report.HasConflicts);
        Assert.Equal(1, report.ShadowedRuleCount);
        var shadowConflict = report.Conflicts.First(c => c.ConflictType == RuleConflictType.ShadowedRule);
        Assert.Equal(rule1.Id, shadowConflict.PrimaryRuleId);
        Assert.Equal(rule2.Id, shadowConflict.SecondaryRuleId);
        Assert.Contains("shadowed", shadowConflict.Description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Reorder execution priority", shadowConflict.Recommendation);
    }

    [Fact]
    public void AnalyzeRules_WhenMonitoredFoldersAreDisjoint_ReportsNoConflicts()
    {
        // Arrange
        var rule1 = new Rule
        {
            Name = "Folder 1 PDFs",
            IsEnabled = true,
            MonitoredFolders = [new MonitoredFolder { Path = @"C:\Folder1" }],
            Conditions = new ConditionGroup { Conditions = [new FileExtensionCondition { Value = "pdf" }] },
            Actions = [new MoveFileAction { DestinationPath = @"C:\Target1" }]
        };

        var rule2 = new Rule
        {
            Name = "Folder 2 PDFs",
            IsEnabled = true,
            MonitoredFolders = [new MonitoredFolder { Path = @"C:\Folder2" }],
            Conditions = new ConditionGroup { Conditions = [new FileExtensionCondition { Value = "pdf" }] },
            Actions = [new MoveFileAction { DestinationPath = @"C:\Target2" }]
        };

        // Act
        var report = _analyzer.AnalyzeRules([rule1, rule2]);

        // Assert
        Assert.False(report.HasConflicts);
        Assert.Empty(report.Conflicts);
    }

    [Fact]
    public void AnalyzeRules_WhenExtensionsAreMutuallyExclusive_ReportsNoConflicts()
    {
        // Arrange: Same folder, but disjoint extensions (.pdf vs .jpg)
        var rule1 = new Rule
        {
            Name = "PDFs",
            IsEnabled = true,
            MonitoredFolders = [new MonitoredFolder { Path = @"C:\Common" }],
            Conditions = new ConditionGroup { Conditions = [new FileExtensionCondition { Value = "pdf" }] },
            Actions = [new MoveFileAction { DestinationPath = @"C:\Documents" }]
        };

        var rule2 = new Rule
        {
            Name = "Images",
            IsEnabled = true,
            MonitoredFolders = [new MonitoredFolder { Path = @"C:\Common" }],
            Conditions = new ConditionGroup { Conditions = [new FileExtensionCondition { Value = "jpg" }] },
            Actions = [new MoveFileAction { DestinationPath = @"C:\Pictures" }]
        };

        // Act
        var report = _analyzer.AnalyzeRules([rule1, rule2]);

        // Assert
        Assert.False(report.HasConflicts);
        Assert.Empty(report.Conflicts);
    }

    [Fact]
    public void AnalyzeRules_WhenRuleIsDisabled_DoesNotProduceConflict()
    {
        // Arrange: One rule is disabled
        var rule1 = new Rule
        {
            Name = "Active Rule",
            IsEnabled = true,
            MonitoredFolders = [new MonitoredFolder { Path = @"C:\Common" }],
            Conditions = new ConditionGroup { Conditions = [new FileExtensionCondition { Value = "pdf" }] },
            Actions = [new MoveFileAction { DestinationPath = @"C:\Dest1" }]
        };

        var rule2 = new Rule
        {
            Name = "Disabled Conflicting Rule",
            IsEnabled = false,
            MonitoredFolders = [new MonitoredFolder { Path = @"C:\Common" }],
            Conditions = new ConditionGroup { Conditions = [new FileExtensionCondition { Value = "pdf" }] },
            Actions = [new MoveFileAction { DestinationPath = @"C:\Dest2" }]
        };

        // Act
        var report = _analyzer.AnalyzeRules([rule1, rule2]);

        // Assert
        Assert.False(report.HasConflicts);
    }
}

