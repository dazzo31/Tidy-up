using FluentAssertions;
using Moq;
using TidyUp.Data.Repositories;
using TidyUp.Models.Domain;
using TidyUp.Models.Enums;
using TidyUp.Services;
using TidyUp.Services.Diagnostics;
using TidyUp.ViewModels;

namespace TidyUp.Tests.Unit.Services;

public class ExplainabilityTests : IDisposable
{
    private readonly string _tempDir;
    private readonly RuleEngine _engine;

    public ExplainabilityTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "TidyUp_ExplainTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
        _engine = new RuleEngine();
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    private FileInfo CreateFile(string name, long sizeBytes = 1024, DateTime? modifiedTime = null)
    {
        var path = Path.Combine(_tempDir, name);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            if (sizeBytes > 0)
            {
                fs.SetLength(sizeBytes);
            }
        }

        if (modifiedTime.HasValue)
        {
            File.SetLastWriteTime(path, modifiedTime.Value);
        }

        return new FileInfo(path);
    }

    [Fact]
    public void ExplainEvaluation_DisabledRule_ReturnsFalseAndExplainsDisabled()
    {
        // Arrange
        var file = CreateFile("document.pdf");
        var rule = new Rule
        {
            Name = "Disabled Rule",
            IsEnabled = false
        };

        // Act
        var result = _engine.ExplainEvaluation(rule, file);

        // Assert
        result.IsRuleEnabled.Should().BeFalse();
        result.IsOverallMatch.Should().BeFalse();
        result.Summary.Should().Contain("disabled");
    }

    [Fact]
    public void ExplainEvaluation_EmptyConditions_ReturnsTrueAndExplainsMatchesAll()
    {
        // Arrange
        var file = CreateFile("document.pdf");
        var rule = new Rule
        {
            Name = "Catch All Rule",
            IsEnabled = true,
            Conditions = new ConditionGroup()
        };

        // Act
        var result = _engine.ExplainEvaluation(rule, file);

        // Assert
        result.IsRuleEnabled.Should().BeTrue();
        result.IsOverallMatch.Should().BeTrue();
        result.Summary.Should().Contain("matches all files by default");
    }

    [Fact]
    public void ExplainEvaluation_FileNameCondition_Is_MatchAndMismatch()
    {
        // Arrange
        var matchFile = CreateFile("Report2026.docx");
        var mismatchFile = CreateFile("Invoice2026.docx");

        var rule = new Rule
        {
            Name = "Report Rule",
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileNameCondition
                    {
                        Operator = StringOperator.Is,
                        Value = "Report2026"
                    }
                }
            }
        };

        // Act Match
        var matchResult = _engine.ExplainEvaluation(rule, matchFile);

        // Assert Match
        matchResult.IsOverallMatch.Should().BeTrue();
        matchResult.Conditions.Should().HaveCount(1);
        matchResult.Conditions[0].IsMatch.Should().BeTrue();
        matchResult.Conditions[0].ConditionType.Should().Be("FileName");
        matchResult.Conditions[0].Explanation.Should().Contain("equals expected 'Report2026'");

        // Act Mismatch
        var mismatchResult = _engine.ExplainEvaluation(rule, mismatchFile);

        // Assert Mismatch
        mismatchResult.IsOverallMatch.Should().BeFalse();
        mismatchResult.Conditions[0].IsMatch.Should().BeFalse();
        mismatchResult.Conditions[0].Explanation.Should().Contain("does not equal expected 'Report2026'");
    }

    [Fact]
    public void ExplainEvaluation_FileExtensionCondition_Is_MatchAndMismatch()
    {
        // Arrange
        var pdfFile = CreateFile("archive.pdf");
        var txtFile = CreateFile("notes.txt");

        var rule = new Rule
        {
            Name = "PDF Rule",
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileExtensionCondition
                    {
                        Operator = StringOperator.Is,
                        Value = "pdf"
                    }
                }
            }
        };

        // Act & Assert
        var pdfResult = _engine.ExplainEvaluation(rule, pdfFile);
        pdfResult.IsOverallMatch.Should().BeTrue();
        pdfResult.Conditions[0].IsMatch.Should().BeTrue();
        pdfResult.Conditions[0].Explanation.Should().Contain("equals expected '.pdf'");

        var txtResult = _engine.ExplainEvaluation(rule, txtFile);
        txtResult.IsOverallMatch.Should().BeFalse();
        txtResult.Conditions[0].IsMatch.Should().BeFalse();
        txtResult.Conditions[0].Explanation.Should().Contain("does not match expected '.pdf'");
    }

    [Fact]
    public void ExplainEvaluation_FileSizeCondition_GreaterThan_MatchAndMismatch()
    {
        // Arrange: 5MB file vs 500KB file
        var bigFile = CreateFile("large_archive.zip", sizeBytes: 5 * 1024 * 1024);
        var smallFile = CreateFile("small_file.zip", sizeBytes: 500 * 1024);

        var rule = new Rule
        {
            Name = "Large File Rule",
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileSizeCondition
                    {
                        Operator = FileSizeCondition.SizeOperator.GreaterThan,
                        Value = 1 * 1024 * 1024 // 1 MB
                    }
                }
            }
        };

        // Act & Assert Big File
        var bigResult = _engine.ExplainEvaluation(rule, bigFile);
        bigResult.IsOverallMatch.Should().BeTrue();
        bigResult.Conditions[0].IsMatch.Should().BeTrue();
        bigResult.Conditions[0].Explanation.Should().Contain("greater than");

        // Act & Assert Small File
        var smallResult = _engine.ExplainEvaluation(rule, smallFile);
        smallResult.IsOverallMatch.Should().BeFalse();
        smallResult.Conditions[0].IsMatch.Should().BeFalse();
        smallResult.Conditions[0].Explanation.Should().Contain("not greater than");
    }

    [Fact]
    public void ExplainEvaluation_FileSizeCondition_Between_MatchAndMismatch()
    {
        // Arrange
        var midFile = CreateFile("medium.dat", sizeBytes: 2 * 1024 * 1024); // 2 MB
        var tooBigFile = CreateFile("huge.dat", sizeBytes: 10 * 1024 * 1024); // 10 MB

        var rule = new Rule
        {
            Name = "Medium Rule",
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileSizeCondition
                    {
                        Operator = FileSizeCondition.SizeOperator.Between,
                        Value = 1 * 1024 * 1024,      // 1 MB min
                        MaxValue = 5 * 1024 * 1024   // 5 MB max
                    }
                }
            }
        };

        // Act & Assert
        var midResult = _engine.ExplainEvaluation(rule, midFile);
        midResult.IsOverallMatch.Should().BeTrue();
        midResult.Conditions[0].IsMatch.Should().BeTrue();
        midResult.Conditions[0].Explanation.Should().Contain("is between");

        var tooBigResult = _engine.ExplainEvaluation(rule, tooBigFile);
        tooBigResult.IsOverallMatch.Should().BeFalse();
        tooBigResult.Conditions[0].IsMatch.Should().BeFalse();
        tooBigResult.Conditions[0].Explanation.Should().Contain("outside range");
    }

    [Fact]
    public void ExplainEvaluation_FileDateCondition_OlderThanDays_MatchAndMismatch()
    {
        // Arrange
        var oldFile = CreateFile("old.log", modifiedTime: DateTime.Now.AddDays(-60));
        var freshFile = CreateFile("fresh.log", modifiedTime: DateTime.Now.AddDays(-2));

        var rule = new Rule
        {
            Name = "Stale Logs Rule",
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileDateCondition
                    {
                        Type = FileDateCondition.DateType.Modified,
                        Operator = FileDateCondition.DateOperator.OlderThanDays,
                        DaysOld = 30
                    }
                }
            }
        };

        // Act & Assert Old
        var oldResult = _engine.ExplainEvaluation(rule, oldFile);
        oldResult.IsOverallMatch.Should().BeTrue();
        oldResult.Conditions[0].IsMatch.Should().BeTrue();
        oldResult.Conditions[0].Explanation.Should().Contain("is older than 30 days");

        // Act & Assert Fresh
        var freshResult = _engine.ExplainEvaluation(rule, freshFile);
        freshResult.IsOverallMatch.Should().BeFalse();
        freshResult.Conditions[0].IsMatch.Should().BeFalse();
        freshResult.Conditions[0].Explanation.Should().Contain("not older than 30 days");
    }

    [Fact]
    public void ExplainEvaluation_ConditionGroup_AndLogic_FailsIfAnyFails()
    {
        // Arrange: file is PDF (match) but only 100 bytes (size mismatch)
        var file = CreateFile("scan.pdf", sizeBytes: 100);

        var rule = new Rule
        {
            Name = "Large PDF Rule",
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.And,
                Conditions =
                {
                    new FileExtensionCondition { Operator = StringOperator.Is, Value = "pdf" },
                    new FileSizeCondition { Operator = FileSizeCondition.SizeOperator.GreaterThan, Value = 1024 * 1024 }
                }
            }
        };

        // Act
        var result = _engine.ExplainEvaluation(rule, file);

        // Assert
        result.IsOverallMatch.Should().BeFalse();
        result.Conditions.Should().HaveCount(2);
        result.Conditions[0].IsMatch.Should().BeTrue();  // Extension matched
        result.Conditions[1].IsMatch.Should().BeFalse(); // Size failed
        result.Summary.Should().Contain("rejected");
        result.Summary.Should().Contain("not greater than");
    }

    [Fact]
    public void ExplainEvaluation_ConditionGroup_OrLogic_SucceedsIfAnyMatches()
    {
        // Arrange: file is JPG (satisfies JPG in OR group)
        var file = CreateFile("photo.jpg");

        var rule = new Rule
        {
            Name = "Image Rule",
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Operator = LogicOperator.Or,
                Conditions =
                {
                    new FileExtensionCondition { Operator = StringOperator.Is, Value = "png" },
                    new FileExtensionCondition { Operator = StringOperator.Is, Value = "jpg" }
                }
            }
        };

        // Act
        var result = _engine.ExplainEvaluation(rule, file);

        // Assert
        result.IsOverallMatch.Should().BeTrue();
        result.Conditions.Should().HaveCount(2);
        result.Conditions[0].IsMatch.Should().BeFalse(); // png failed
        result.Conditions[1].IsMatch.Should().BeTrue();  // jpg matched
        result.Summary.Should().Contain("Or condition satisfied");
    }

    [Fact]
    public void ExplainEvaluation_NestedConditionGroups_EvaluatesRecursively()
    {
        // Arrange: (Extension is .pdf OR .docx) AND (Size > 1KB)
        var file = CreateFile("document.docx", sizeBytes: 2048);

        var extGroup = new ConditionGroup
        {
            Operator = LogicOperator.Or,
            Conditions =
            {
                new FileExtensionCondition { Operator = StringOperator.Is, Value = "pdf" },
                new FileExtensionCondition { Operator = StringOperator.Is, Value = "docx" }
            }
        };

        var rootGroup = new ConditionGroup
        {
            Operator = LogicOperator.And,
            Conditions =
            {
                extGroup,
                new FileSizeCondition { Operator = FileSizeCondition.SizeOperator.GreaterThan, Value = 1000 }
            }
        };

        var rule = new Rule
        {
            Name = "Office Documents Rule",
            IsEnabled = true,
            Conditions = rootGroup
        };

        // Act
        var result = _engine.ExplainEvaluation(rule, file);

        // Assert
        result.IsOverallMatch.Should().BeTrue();
        result.Conditions.Should().HaveCount(2);

        // Child 0 is ConditionGroup
        var childGroup = result.Conditions[0];
        childGroup.ConditionType.Should().Be("ConditionGroup");
        childGroup.IsMatch.Should().BeTrue();
        childGroup.Children.Should().HaveCount(2);
        childGroup.Children[0].IsMatch.Should().BeFalse(); // pdf
        childGroup.Children[1].IsMatch.Should().BeTrue();  // docx

        // Child 1 is FileSize
        result.Conditions[1].IsMatch.Should().BeTrue();
    }

    [Fact]
    public async Task RuleEvaluationInspectorViewModel_InspectFile_PopulatesDiagnostics()
    {
        // Arrange
        var testFile = CreateFile("budget.xlsx");
        var rule = new Rule
        {
            Name = "Spreadsheets",
            IsEnabled = true,
            Conditions = new ConditionGroup
            {
                Conditions = { new FileExtensionCondition { Operator = StringOperator.Is, Value = "xlsx" } }
            }
        };

        var mockRepo = new Mock<IRuleRepository>();
        mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<Rule> { rule });

        var vm = new RuleEvaluationInspectorViewModel(_engine, mockRepo.Object);

        await vm.LoadRulesAsync();
        vm.AvailableRules.Should().HaveCount(1);
        vm.SelectedRule = rule;
        vm.TargetFilePath = testFile.FullName;
        vm.CanInspect.Should().BeTrue();

        // Act
        vm.InspectFile();

        // Assert
        vm.HasDiagnostics.Should().BeTrue();
        vm.Diagnostics.Should().NotBeNull();
        vm.IsMatch.Should().BeTrue();
        vm.ResultSummary.Should().Contain("matches rule 'Spreadsheets'");
    }
}

