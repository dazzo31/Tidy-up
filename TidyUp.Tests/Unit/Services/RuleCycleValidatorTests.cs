using FluentAssertions;
using System.IO;
using Microsoft.EntityFrameworkCore;
using TidyUp.Data;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.Validation;
using TidyUp.Tests.Helpers;

namespace TidyUp.Tests.Unit.Services;

public class RuleCycleValidatorTests : IDisposable
{
    private readonly TestFileHelper _fileHelper;
    private readonly RuleValidator _validator;
    private readonly VariableEngine _variableEngine;

    public RuleCycleValidatorTests()
    {
        _fileHelper = new TestFileHelper();
        _variableEngine = new VariableEngine();
        _validator = new RuleValidator(_variableEngine);
    }

    public void Dispose()
    {
        _fileHelper.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Single Rule Validation Tests

    [Fact]
    public void ValidateRule_Detects_IdenticalSourceAndDestination()
    {
        // Arrange
        var folder = Path.Combine(_fileHelper.TestRootDirectory, "Downloads");
        var rule = new Rule
        {
            Name = "Infinite Loop Move",
            MonitoredFolders = { new MonitoredFolder { Path = folder, IncludeSubfolders = false } },
            Actions = { new MoveFileAction { DestinationPath = folder } }
        };

        // Act
        var result = _validator.ValidateRule(rule);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == RuleValidationErrorCode.IdenticalSourceAndDestination);
        result.Errors[0].ErrorMessage.Should().Contain("identical to monitored source folder");
    }

    [Fact]
    public void ValidateRule_Detects_DestinationIsSubfolderOfMonitoredSource_WhenIncludeSubfoldersIsTrue()
    {
        // Arrange
        var parentFolder = Path.Combine(_fileHelper.TestRootDirectory, "Downloads");
        var subFolder = Path.Combine(parentFolder, "Organized");
        var rule = new Rule
        {
            Name = "Nested Recursive Move",
            MonitoredFolders = { new MonitoredFolder { Path = parentFolder, IncludeSubfolders = true } },
            Actions = { new MoveFileAction { DestinationPath = subFolder } }
        };

        // Act
        var result = _validator.ValidateRule(rule);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == RuleValidationErrorCode.DestinationIsSubfolderOfMonitoredSource);
        result.Errors[0].ErrorMessage.Should().Contain("infinite recursive loop");
    }

    [Fact]
    public void ValidateRule_Warns_DestinationIsSubfolder_WhenIncludeSubfoldersIsFalse()
    {
        // Arrange
        var parentFolder = Path.Combine(_fileHelper.TestRootDirectory, "Downloads");
        var subFolder = Path.Combine(parentFolder, "Organized");
        var rule = new Rule
        {
            Name = "Non-recursive Nested Move",
            MonitoredFolders = { new MonitoredFolder { Path = parentFolder, IncludeSubfolders = false } },
            Actions = { new MoveFileAction { DestinationPath = subFolder } }
        };

        // Act
        var result = _validator.ValidateRule(rule);

        // Assert
        result.IsValid.Should().BeTrue("not an automatic recursion loop if subfolder monitoring is disabled");
        result.HasWarnings.Should().BeTrue();
        result.Warnings.Should().ContainSingle(w => w.WarningMessage.Contains("located inside monitored folder"));
    }

    [Fact]
    public void ValidateRule_Passes_WhenSourceAndDestinationAreIndependent()
    {
        // Arrange
        var sourceFolder = Path.Combine(_fileHelper.TestRootDirectory, "Incoming");
        var destFolder = Path.Combine(_fileHelper.TestRootDirectory, "Sorted");
        var rule = new Rule
        {
            Name = "Clean Independent Move",
            MonitoredFolders = { new MonitoredFolder { Path = sourceFolder, IncludeSubfolders = true } },
            Actions = { new MoveFileAction { DestinationPath = destFolder } }
        };

        // Act
        var result = _validator.ValidateRule(rule);

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("PRN")]
    [InlineData("AUX")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT1")]
    public void ValidateRule_Rejects_ReservedWindowsDeviceNames(string reservedDeviceName)
    {
        // Arrange
        var safeSource = Path.Combine(_fileHelper.TestRootDirectory, "SafeSource");
        var illegalDest = Path.Combine(_fileHelper.TestRootDirectory, reservedDeviceName);
        var rule = new Rule
        {
            Name = "Device Name Conflict",
            MonitoredFolders = { new MonitoredFolder { Path = safeSource } },
            Actions = { new MoveFileAction { DestinationPath = illegalDest } }
        };

        // Act
        var result = _validator.ValidateRule(rule);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == RuleValidationErrorCode.ReservedDeviceName);
        result.Errors[0].ErrorMessage.Should().Contain("reserved Windows device name");
    }

    [Fact]
    public void ValidateRule_Rejects_DirectoryTraversal()
    {
        // Arrange
        var source = Path.Combine(_fileHelper.TestRootDirectory, "Source");
        var traversalDest = Path.Combine(source, "..", "EscapedFolder");
        var rule = new Rule
        {
            Name = "Traversal Rule",
            MonitoredFolders = { new MonitoredFolder { Path = source } },
            Actions = { new MoveFileAction { DestinationPath = traversalDest } }
        };

        // Act
        var result = _validator.ValidateRule(rule);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.ErrorCode == RuleValidationErrorCode.DirectoryTraversal);
    }

    [Fact]
    public void ValidateRule_Rejects_EmptyRuleName_AndEmptyMonitoredFolders()
    {
        // Arrange
        var ruleNoName = new Rule
        {
            Name = "   ",
            MonitoredFolders = { new MonitoredFolder { Path = "C:\\Valid\\Path" } },
            Actions = { new MoveFileAction { DestinationPath = "C:\\Other\\Path" } }
        };

        var ruleNoFolders = new Rule
        {
            Name = "Valid Name",
            MonitoredFolders = { },
            Actions = { new MoveFileAction { DestinationPath = "C:\\Other\\Path" } }
        };

        // Act
        var res1 = _validator.ValidateRule(ruleNoName);
        var res2 = _validator.ValidateRule(ruleNoFolders);

        // Assert
        res1.IsValid.Should().BeFalse();
        res1.Errors.Should().ContainSingle(e => e.ErrorCode == RuleValidationErrorCode.EmptyRuleName);

        res2.IsValid.Should().BeFalse();
        res2.Errors.Should().ContainSingle(e => e.ErrorCode == RuleValidationErrorCode.MissingMonitoredFolders);
    }

    #endregion

    #region Cross-Rule Cycle Validation Tests

    [Fact]
    public void ValidateRules_Detects_TwoRulePingPongCycle()
    {
        // Arrange
        var folderA = Path.Combine(_fileHelper.TestRootDirectory, "FolderA");
        var folderB = Path.Combine(_fileHelper.TestRootDirectory, "FolderB");

        var ruleAtoB = new Rule
        {
            Name = "Rule A to B",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = folderA } },
            Actions = { new MoveFileAction { DestinationPath = folderB } }
        };

        var ruleBtoA = new Rule
        {
            Name = "Rule B to A",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = folderB } },
            Actions = { new MoveFileAction { DestinationPath = folderA } }
        };

        // Act
        var result = _validator.ValidateRules(new[] { ruleAtoB, ruleBtoA });

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == RuleValidationErrorCode.CrossRuleCycleDetected);
        result.Errors.Any(e => e.ErrorMessage.Contains("Rule A to B") && e.ErrorMessage.Contains("Rule B to A")).Should().BeTrue();
    }

    [Fact]
    public void ValidateRules_Detects_ThreeRuleCycle()
    {
        // Arrange: A -> B -> C -> A
        var folderA = Path.Combine(_fileHelper.TestRootDirectory, "FolderA");
        var folderB = Path.Combine(_fileHelper.TestRootDirectory, "FolderB");
        var folderC = Path.Combine(_fileHelper.TestRootDirectory, "FolderC");

        var rule1 = new Rule
        {
            Name = "Rule 1 (A->B)",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = folderA } },
            Actions = { new MoveFileAction { DestinationPath = folderB } }
        };

        var rule2 = new Rule
        {
            Name = "Rule 2 (B->C)",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = folderB } },
            Actions = { new MoveFileAction { DestinationPath = folderC } }
        };

        var rule3 = new Rule
        {
            Name = "Rule 3 (C->A)",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = folderC } },
            Actions = { new MoveFileAction { DestinationPath = folderA } }
        };

        // Act
        var result = _validator.ValidateRules(new[] { rule1, rule2, rule3 });

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorCode == RuleValidationErrorCode.CrossRuleCycleDetected);
    }

    [Fact]
    public void ValidateRules_Passes_ForLinearRuleChains()
    {
        // Arrange: A -> B -> C (no loop)
        var folderA = Path.Combine(_fileHelper.TestRootDirectory, "FolderA");
        var folderB = Path.Combine(_fileHelper.TestRootDirectory, "FolderB");
        var folderC = Path.Combine(_fileHelper.TestRootDirectory, "FolderC");

        var rule1 = new Rule
        {
            Name = "Rule 1",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = folderA } },
            Actions = { new MoveFileAction { DestinationPath = folderB } }
        };

        var rule2 = new Rule
        {
            Name = "Rule 2",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = folderB } },
            Actions = { new MoveFileAction { DestinationPath = folderC } }
        };

        // Act
        var result = _validator.ValidateRules(new[] { rule1, rule2 });

        // Assert
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task RuleRepository_RejectsSaving_RuleWithCyclicLoop()
    {
        // Arrange
        var dbPath = Path.Combine(_fileHelper.TestRootDirectory, "repo_val_test.db");
        var options = new DbContextOptionsBuilder<TidyUpDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;
        using var dbContext = new TidyUpDbContext(options);
        dbContext.Database.EnsureCreated();

        var repo = new RuleRepository(dbContext, _validator);

        var folder = Path.Combine(_fileHelper.TestRootDirectory, "SameFolder");
        var badRule = new Rule
        {
            Name = "Illegal Self-Move",
            IsEnabled = true,
            MonitoredFolders = { new MonitoredFolder { Path = folder } },
            Actions = { new MoveFileAction { DestinationPath = folder } }
        };

        // Act
        var act = () => repo.AddAsync(badRule);

        // Assert
        await act.Should().ThrowAsync<RuleValidationException>()
            .Where(ex => ex.ValidationErrors.Any(e => e.ErrorCode == RuleValidationErrorCode.IdenticalSourceAndDestination));
    }

    #endregion
}

