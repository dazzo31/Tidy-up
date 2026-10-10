using FluentAssertions;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services.Rules;

namespace TidyUp.Tests.Unit.Services;

public class RuleSummaryGeneratorTests
{
    private readonly RuleSummaryGenerator _generator;

    public RuleSummaryGeneratorTests()
    {
        _generator = new RuleSummaryGenerator();
    }

    #region Full Rule Summary Tests

    [Fact]
    public void GenerateSummary_NullRule_ReturnsFallbackString()
    {
        // Act
        var result = _generator.GenerateSummary(null!);

        // Assert
        result.Should().Be("No rule provided.");
    }

    [Fact]
    public void GenerateSummary_RuleWithNoConditionsNoFoldersNoActions_ProducesDefaultProse()
    {
        // Arrange
        var rule = new Rule
        {
            Name = "Empty Rule"
        };

        // Act
        var result = _generator.GenerateSummary(rule);

        // Assert
        result.Should().Be("When a file is detected, no actions are executed.");
    }

    [Fact]
    public void GenerateSummary_SingleCondition_SingleFolder_SingleAction()
    {
        // Arrange
        var rule = new Rule
        {
            Name = "PDF Mover",
            MonitoredFolders =
            {
                new MonitoredFolder { Path = @"C:\Users\TestUser\Downloads" }
            },
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileExtensionCondition { Operator = StringOperator.Is, Value = "pdf" }
                }
            },
            Actions =
            {
                new MoveFileAction
                {
                    DestinationPath = @"D:\Documents\PDFs",
                    ConflictResolution = ConflictResolution.Overwrite,
                    Order = 0
                }
            }
        };

        // Act
        var result = _generator.GenerateSummary(rule);

        // Assert
        result.Should().Be("When a file where extension is 'pdf' is detected in 'Downloads', move to 'PDFs' (overwriting if exists).");
    }

    [Fact]
    public void GenerateSummary_MultipleFolders_UsesOrConjunction()
    {
        // Arrange
        var rule = new Rule
        {
            Name = "Multi Folder Rule",
            MonitoredFolders =
            {
                new MonitoredFolder { Path = @"C:\Folder1" },
                new MonitoredFolder { Path = @"C:\Folder2" }
            },
            Actions =
            {
                new DeleteFileAction { UseRecycleBin = true, Order = 0 }
            }
        };

        // Act
        var result = _generator.GenerateSummary(rule);

        // Assert
        result.Should().Be("When a file is detected in 'Folder1' or 'Folder2', send to Recycle Bin.");
    }

    [Fact]
    public void GenerateSummary_ThreeFolders_UsesCommaSeparationAndOr()
    {
        // Arrange
        var rule = new Rule
        {
            Name = "Three Folders",
            MonitoredFolders =
            {
                new MonitoredFolder { Path = @"C:\FolderA" },
                new MonitoredFolder { Path = @"C:\FolderB" },
                new MonitoredFolder { Path = @"C:\FolderC" }
            },
            Actions =
            {
                new DeleteFileAction { UseRecycleBin = true, Order = 0 }
            }
        };

        // Act
        var result = _generator.GenerateSummary(rule);

        // Assert
        result.Should().Be("When a file is detected in 'FolderA', 'FolderB' or 'FolderC', send to Recycle Bin.");
    }

    [Fact]
    public void GenerateSummary_MultipleActions_TwoActions_JoinedWithAnd()
    {
        // Arrange
        var rule = new Rule
        {
            Name = "Two Actions Rule",
            MonitoredFolders = { new MonitoredFolder { Path = @"C:\DropBox" } },
            Actions =
            {
                new MoveFileAction { DestinationPath = @"C:\Processed", ConflictResolution = ConflictResolution.Skip, Order = 1 },
                new RenameFileAction { NamePattern = "{name}_{date}", Order = 2 }
            }
        };

        // Act
        var result = _generator.GenerateSummary(rule);

        // Assert
        result.Should().Be("When a file is detected in 'DropBox', move to 'Processed' (skipping if conflict) and rename using pattern '{name}_{date}'.");
    }

    [Fact]
    public void GenerateSummary_MultipleActions_ThreeActions_UsesOxfordComma()
    {
        // Arrange
        var rule = new Rule
        {
            Name = "Three Actions Rule",
            MonitoredFolders = { new MonitoredFolder { Path = @"C:\Inbox" } },
            Actions =
            {
                new RenameFileAction { NamePattern = "doc_{name}", Order = 1 },
                new ChangeExtensionAction { NewExtension = "bak", Order = 2 },
                new MoveFileAction { DestinationPath = @"C:\Archive", ConflictResolution = ConflictResolution.RenameNew, Order = 3 }
            }
        };

        // Act
        var result = _generator.GenerateSummary(rule);

        // Assert
        result.Should().Be("When a file is detected in 'Inbox', rename using pattern 'doc_{name}', change extension to '.bak', and move to 'Archive' (renaming if conflict).");
    }

    #endregion

    #region Condition Summary Tests

    [Fact]
    public void GenerateConditionSummary_EmptyOrNullGroup_ReturnsEmptyString()
    {
        _generator.GenerateConditionSummary(null).Should().BeEmpty();
        _generator.GenerateConditionSummary(new ConditionGroup()).Should().BeEmpty();
    }

    [Fact]
    public void GenerateConditionSummary_AndOperator_JoinsWithAnd()
    {
        // Arrange
        var group = new ConditionGroup
        {
            Operator = LogicOperator.And,
            Conditions =
            {
                new FileExtensionCondition { Operator = StringOperator.Is, Value = ".pdf" },
                new FileSizeCondition { Operator = FileSizeCondition.SizeOperator.GreaterThan, Value = 5 * 1024 * 1024 }
            }
        };

        // Act
        var result = _generator.GenerateConditionSummary(group);

        // Assert
        result.Should().Be("extension is 'pdf' and size is greater than 5.0 MB");
    }

    [Fact]
    public void GenerateConditionSummary_OrOperator_JoinsWithOr()
    {
        // Arrange
        var group = new ConditionGroup
        {
            Operator = LogicOperator.Or,
            Conditions =
            {
                new FileExtensionCondition { Operator = StringOperator.Is, Value = "jpg" },
                new FileExtensionCondition { Operator = StringOperator.Is, Value = "png" }
            }
        };

        // Act
        var result = _generator.GenerateConditionSummary(group);

        // Assert
        result.Should().Be("extension is 'jpg' or extension is 'png'");
    }

    [Fact]
    public void GenerateConditionSummary_NestedConditionGroup_WrapsInParentheses()
    {
        // Arrange: extension is 'pdf' and (name contains 'report' or name contains 'invoice')
        var nestedOrGroup = new ConditionGroup
        {
            Operator = LogicOperator.Or,
            Conditions =
            {
                new FileNameCondition { Operator = StringOperator.Contains, Value = "report" },
                new FileNameCondition { Operator = StringOperator.Contains, Value = "invoice" }
            }
        };

        var rootGroup = new ConditionGroup
        {
            Operator = LogicOperator.And,
            Conditions =
            {
                new FileExtensionCondition { Operator = StringOperator.Is, Value = "pdf" },
                nestedOrGroup
            }
        };

        // Act
        var result = _generator.GenerateConditionSummary(rootGroup);

        // Assert
        result.Should().Be("extension is 'pdf' and (name contains 'report' or name contains 'invoice')");
    }

    [Theory]
    [InlineData(StringOperator.Is, "name is 'document'")]
    [InlineData(StringOperator.IsNot, "name is not 'document'")]
    [InlineData(StringOperator.Contains, "name contains 'document'")]
    [InlineData(StringOperator.DoesNotContain, "name does not contain 'document'")]
    [InlineData(StringOperator.StartsWith, "name starts with 'document'")]
    [InlineData(StringOperator.EndsWith, "name ends with 'document'")]
    [InlineData(StringOperator.MatchesRegex, "name matches regex 'document'")]
    [InlineData(StringOperator.IsEmpty, "name is empty")]
    public void SummarizeCondition_FileNameCondition_HandlesAllOperators(StringOperator op, string expected)
    {
        var group = new ConditionGroup
        {
            Conditions = { new FileNameCondition { Operator = op, Value = "document" } }
        };

        var result = _generator.GenerateConditionSummary(group);
        result.Should().Be(expected);
    }

    [Fact]
    public void SummarizeCondition_FileSizeCondition_HandlesBetweenAndFormatting()
    {
        var group = new ConditionGroup
        {
            Conditions =
            {
                new FileSizeCondition
                {
                    Operator = FileSizeCondition.SizeOperator.Between,
                    Value = 1024,
                    MaxValue = 5 * 1024 * 1024
                }
            }
        };

        var result = _generator.GenerateConditionSummary(group);
        result.Should().Be("size is between 1.0 KB and 5.0 MB");
    }

    [Fact]
    public void SummarizeCondition_DateCondition_HandlesCreatedAndModified()
    {
        var targetDate = new DateTime(2026, 1, 15);
        var group = new ConditionGroup
        {
            Operator = LogicOperator.And,
            Conditions =
            {
                new FileDateCondition
                {
                    Type = FileDateCondition.DateType.Created,
                    Operator = FileDateCondition.DateOperator.After,
                    Value = targetDate
                },
                new FileDateCondition
                {
                    Type = FileDateCondition.DateType.Modified,
                    Operator = FileDateCondition.DateOperator.OlderThanDays,
                    DaysOld = 45
                }
            }
        };

        var result = _generator.GenerateConditionSummary(group);
        result.Should().Be("created date is after 2026-01-15 and modified date is older than 45 days");
    }

    #endregion

    #region Action Summary Tests

    [Fact]
    public void GenerateActionSummary_DeleteAction_RecycleBinAndEmptyFolders()
    {
        var actionWithBoth = new DeleteFileAction { UseRecycleBin = true, RemoveEmptyFolders = true };
        var actionPermanent = new DeleteFileAction { UseRecycleBin = false, RemoveEmptyFolders = false };

        _generator.GenerateActionSummary(actionWithBoth).Should().Be("send to Recycle Bin and remove empty parent folders");
        _generator.GenerateActionSummary(actionPermanent).Should().Be("permanently delete");
    }

    [Fact]
    public void GenerateActionSummary_ExtractAndRunCommandActions()
    {
        var extract = new ExtractArchiveAction { DestinationPath = @"C:\ExtractedArchive" };
        var runCommand = new RunCommandAction { Command = "powershell -File backup.ps1" };

        _generator.GenerateActionSummary(extract).Should().Be("extract archive contents to 'ExtractedArchive'");
        _generator.GenerateActionSummary(runCommand).Should().Be("run command 'powershell -File backup.ps1'");
    }

    #endregion
}

