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
}
