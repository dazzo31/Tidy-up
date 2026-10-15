using FluentAssertions;
using TidyUp.Services.Folders;
using Xunit;

namespace TidyUp.Tests.Unit.Services;

public class FolderPathNormalizerTests
{
    private readonly FolderPathNormalizer _normalizer = new();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    public void TryNormalize_EmptyOrWhitespace_ReturnsFalse(string? input)
    {
        var result = _normalizer.TryNormalize(input, out var normalized, out var error);

        result.Should().BeFalse();
        normalized.Should().BeEmpty();
        error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void TryNormalize_RelativePath_ReturnsFalse()
    {
        var result = _normalizer.TryNormalize(@"relative\subfolder", out var normalized, out var error);

        result.Should().BeFalse();
        normalized.Should().BeEmpty();
        error.Should().Contain("must be fully qualified");
    }

    [Fact]
    public void TryNormalize_QuotedPath_TrimsQuotes()
    {
        var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var input = $"\"{tempDir}\"";

        var result = _normalizer.TryNormalize(input, out var normalized, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        normalized.Should().Be(tempDir);
    }

    [Fact]
    public void TryNormalize_TrailingSlash_TrimsSeparator()
    {
        var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var input = tempDir + Path.DirectorySeparatorChar;

        var result = _normalizer.TryNormalize(input, out var normalized, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        normalized.Should().Be(tempDir);
    }

    [Fact]
    public void TryNormalize_DriveRoot_PreservesRootSeparator()
    {
        var root = Path.GetPathRoot(Environment.SystemDirectory); // E.g., "C:\"
        root.Should().NotBeNullOrWhiteSpace();

        var result = _normalizer.TryNormalize(root, out var normalized, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        normalized.Should().Be(root);
    }

    [Fact]
    public void TryNormalize_EnvironmentVariables_ExpandsCorrectly()
    {
        var input = @"%SYSTEMROOT%\System32";
        var expected = Environment.GetFolderPath(Environment.SpecialFolder.System);

        var result = _normalizer.TryNormalize(input, out var normalized, out var error);

        result.Should().BeTrue();
        error.Should().BeNull();
        normalized.Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void AreEqual_SamePathWithDifferentSlashesOrCasing_ReturnsTrue()
    {
        var tempDir = Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var path1 = tempDir.ToLowerInvariant() + @"\";
        var path2 = tempDir.ToUpperInvariant();

        var areEqual = _normalizer.AreEqual(path1, path2);

        areEqual.Should().BeTrue();
    }
}

