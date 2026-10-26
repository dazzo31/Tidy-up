using System.IO;
using TidyUp.Services;
using Xunit;
using FluentAssertions;

namespace TidyUp.Tests.Unit.Services;

public class VariableEngineTests
{
    private readonly VariableEngine _engine;
    private readonly FileInfo _testFile;

    public VariableEngineTests()
    {
        _engine = new VariableEngine();
        
        // Create a temporary test file
        var tempPath = Path.Combine(Path.GetTempPath(), "test_file.txt");
        File.WriteAllText(tempPath, "test content");
        _testFile = new FileInfo(tempPath);
    }

    [Fact]
    public void Resolve_FilenameVariable_ReturnsFileNameWithoutExtension()
    {
        // Arrange
        var template = "{filename}";

        // Act
        var result = _engine.Resolve(template, _testFile);

        // Assert
        result.Should().Be("test_file");
    }

    [Fact]
    public void Resolve_ExtensionVariable_ReturnsExtensionWithoutDot()
    {
        // Arrange
        var template = "{extension}";

        // Act
        var result = _engine.Resolve(template, _testFile);

        // Assert
        result.Should().Be("txt");
    }

    [Fact]
    public void Resolve_MultipleVariables_ReplacesAll()
    {
        // Arrange
        var template = "{filename}.backup.{extension}";

        // Act
        var result = _engine.Resolve(template, _testFile);

        // Assert
        result.Should().Be("test_file.backup.txt");
    }

    [Fact]
    public void Resolve_WithFormatSpecifier_AppliesFormat()
    {
        // Arrange
        var template = "{filename:upper}";

        // Act
        var result = _engine.Resolve(template, _testFile);

        // Assert
        result.Should().Be("TEST_FILE");
    }

    [Fact]
    public void Resolve_CounterVariable_FormatsWithPadding()
    {
        // Arrange
        var template = "{counter:000}_{filename}";

        // Act
        var result = _engine.Resolve(template, _testFile, counter: 5);

        // Assert
        result.Should().Be("005_test_file");
    }

    [Fact]
    public void Resolve_UnknownVariable_LeavesAsIs()
    {
        // Arrange
        var template = "{unknown_variable}";

        // Act
        var result = _engine.Resolve(template, _testFile);

        // Assert
        result.Should().Be("{unknown_variable}");
    }

    [Fact]
    public void IsValidTemplate_WithKnownVariables_ReturnsTrue()
    {
        // Arrange
        var template = "{filename}_{created_date}";

        // Act
        var result = _engine.IsValidTemplate(template);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsValidTemplate_WithUnknownVariable_ReturnsFalse()
    {
        // Arrange
        var template = "{unknown_var}";

        // Act
        var result = _engine.IsValidTemplate(template);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Resolve_TimeVariables_NeverProduceColons_AlwaysHyphens()
    {
        // Arrange
        var template = "{now_time}_{created_time}_{modified_time}_{time}_{datetime}";

        // Act
        var result = _engine.Resolve(template, _testFile);

        // Assert: Colons are forbidden in Windows filenames
        result.Should().NotContain(":", "colons in filenames cause Windows I/O exceptions");
        System.Text.RegularExpressions.Regex.IsMatch(result, @"\d{2}-\d{2}-\d{2}").Should().BeTrue();
    }

    [Fact]
    public void Resolve_CustomTimeFormatWithColons_SanitizesColonsToHyphens()
    {
        // Arrange: Explicit user format with colons
        var template = "{now_time:HH:mm:ss}_{date:yyyy:MM:dd}";

        // Act
        var result = _engine.Resolve(template, _testFile);

        // Assert: Must automatically sanitize colons to hyphens
        result.Should().NotContain(":");
    }

    [Fact]
    public void Resolve_CaseInsensitiveMatching_SupportsAllDocumentedTokensAndAliases()
    {
        // Arrange: All tokens advertised in HelpView and Onboarding
        var template = "{FileName}_{FileNameWithExtension}_{FileExtension}_{FileSize}_{FileSizeKB}_{FileSizeMB}_" +
                       "{Year}_{Month}_{MonthName}_{Day}_{Hour}_{Minute}_{Second}_{Date}_{Time}_{DateTime}_" +
                       "{CreationDate}_{ModifiedDate}_{CreationYear}_{ModifiedYear}_{CreationTime}_{ModifiedTime}_" +
                       "{ext}_{extension}_{date}_{year}_{month}_{day}";

        // Act
        var result = _engine.Resolve(template, _testFile);

        // Assert: None should remain as unexpanded {Token}
        result.Should().NotContain("{", "all documented tokens and aliases must resolve");
        result.Should().NotContain("}", "all documented tokens and aliases must resolve");
        result.Should().NotContain(":", "no resolved token should introduce colons");
    }

    [Fact]
    public void IsKnownVariable_ReturnsTrueForDocumentedTokensAndFalseForUnknown()
    {
        _engine.IsKnownVariable("filename").Should().BeTrue();
        _engine.IsKnownVariable("FILENAME").Should().BeTrue();
        _engine.IsKnownVariable("ext").Should().BeTrue();
        _engine.IsKnownVariable("Ext").Should().BeTrue();
        _engine.IsKnownVariable("fileextension").Should().BeTrue();
        _engine.IsKnownVariable("FileExtension").Should().BeTrue();
        _engine.IsKnownVariable("year").Should().BeTrue();
        _engine.IsKnownVariable("Year").Should().BeTrue();
        _engine.IsKnownVariable("month").Should().BeTrue();
        _engine.IsKnownVariable("day").Should().BeTrue();
        _engine.IsKnownVariable("date").Should().BeTrue();
        _engine.IsKnownVariable("time").Should().BeTrue();
        _engine.IsKnownVariable("datetime").Should().BeTrue();
        _engine.IsKnownVariable("now_time").Should().BeTrue();
        _engine.IsKnownVariable("counter").Should().BeTrue();

        _engine.IsKnownVariable("foobar").Should().BeFalse();
        _engine.IsKnownVariable("unknown_token").Should().BeFalse();
        _engine.IsKnownVariable(string.Empty).Should().BeFalse();
    }

    [Fact]
    public void GetUnknownVariables_IdentifiesUnrecognizedTokens()
    {
        var template = "{filename}_{foobar}_{extension}_{bad_token:upper}";

        var unknown = _engine.GetUnknownVariables(template);

        unknown.Should().HaveCount(2);
        unknown.Should().Contain("foobar");
        unknown.Should().Contain("bad_token");
    }

    [Fact]
    public void RuleValidator_WithUnknownVariableToken_BlocksRuleWithValidationError()
    {
        var validator = new TidyUp.Services.Validation.RuleValidator(_engine);
        var rule = new TidyUp.Models.Domain.Rule
        {
            Name = "Rule with bad template",
            MonitoredFolders = { new TidyUp.Models.Domain.MonitoredFolder { Path = @"C:\Monitored" } },
            Actions =
            {
                new TidyUp.Models.Domain.MoveFileAction
                {
                    DestinationPath = @"C:\Destination\{unknown_var}\{now_year}"
                }
            }
        };

        var result = validator.ValidateRule(rule);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e =>
            e.ErrorCode == TidyUp.Services.Validation.RuleValidationErrorCode.UnknownVariableToken &&
            e.ErrorMessage.Contains("{unknown_var}"));
    }

    [Fact]
    public void FirstRunViewModel_SampleRulePattern_ResolvesWithoutLiteralBraces()
    {
        var samplePattern = Path.Combine(@"C:\Users\Test\Documents", "{extension}", "{now_year}-{now_month}");

        var resolved = _engine.Resolve(samplePattern, _testFile);

        resolved.Should().NotContain("{");
        resolved.Should().NotContain("}");
        resolved.Should().Contain("txt");
    }
}
