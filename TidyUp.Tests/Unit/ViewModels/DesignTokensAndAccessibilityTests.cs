using System.IO;
using System.Windows;
using System.Windows.Markup;
using System.Xml.Linq;
using FluentAssertions;
using Xunit;

namespace TidyUp.Tests.Unit.ViewModels;

public class DesignTokensAndAccessibilityTests
{
    private static string GetStyleFilePath(string relativePath)
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dir = new DirectoryInfo(baseDir);
        string? projectDir = null;
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "TidyUp", relativePath);
            if (File.Exists(candidate))
            {
                projectDir = Path.Combine(dir.FullName, "TidyUp");
                break;
            }
            dir = dir.Parent;
        }

        projectDir.Should().NotBeNull($"Directory containing TidyUp should be located from {baseDir}");
        var filePath = Path.Combine(projectDir!, relativePath);
        File.Exists(filePath).Should().BeTrue($"File {filePath} must exist");
        return filePath;
    }

    [Fact]
    public void ColorsDictionary_LoadsSuccessfully_AndContainsCoreTokens()
    {
        var filePath = GetStyleFilePath("Styles/Colors.xaml");
        using var stream = File.OpenRead(filePath);
        var parsed = XamlReader.Load(stream);
        parsed.Should().BeOfType<ResourceDictionary>();
        var dict = (ResourceDictionary)parsed;

        dict.Contains("Win11AccentBrush").Should().BeTrue();
        dict.Contains("Win11SuccessBrush").Should().BeTrue();
        dict.Contains("Win11WarningBrush").Should().BeTrue();
        dict.Contains("Win11ErrorBrush").Should().BeTrue();
        dict.Contains("Win11SubtleBorderBrush").Should().BeTrue();
        dict.Contains("Win11TextPrimaryBrush").Should().BeTrue();
    }

    [Fact]
    public void TypographyDictionary_LoadsSuccessfully_AndContainsTypeScale()
    {
        var filePath = GetStyleFilePath("Styles/Typography.xaml");
        using var stream = File.OpenRead(filePath);
        var parsed = XamlReader.Load(stream);
        parsed.Should().BeOfType<ResourceDictionary>();
        var dict = (ResourceDictionary)parsed;

        dict.Contains("Win11DisplayTextBlock").Should().BeTrue();
        dict.Contains("Win11TitleTextBlock").Should().BeTrue();
        dict.Contains("Win11SubtitleTextBlock").Should().BeTrue();
        dict.Contains("Win11BodyTextBlock").Should().BeTrue();
        dict.Contains("Win11CaptionTextBlock").Should().BeTrue();
        dict.Contains("Win11CodeTextBlock").Should().BeTrue();
    }

    [Fact]
    public void ControlsDictionary_ValidXml_AndContainsWindows11ControlTokens()
    {
        var filePath = GetStyleFilePath("Styles/Controls.xaml");
        var doc = XDocument.Load(filePath);
        var keys = doc.Descendants().Attributes(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))
            .Select(a => a.Value).ToList();

        keys.Should().Contain("Win11Card");
        keys.Should().Contain("Win11PrimaryButton");
        keys.Should().Contain("Win11SecondaryButton");
        keys.Should().Contain("Win11StatusBadge");
    }

    [Fact]
    public void AccessibilityStyles_ValidXml_AndContainsAccessibleTokens()
    {
        var filePath = GetStyleFilePath("Styles/AccessibilityStyles.xaml");
        var doc = XDocument.Load(filePath);
        var keys = doc.Descendants().Attributes(XName.Get("Key", "http://schemas.microsoft.com/winfx/2006/xaml"))
            .Select(a => a.Value).ToList();

        keys.Should().Contain("AccessibleFocusVisual");
        keys.Should().Contain("AccessibleHeadlineTextBlock");
        keys.Should().Contain("AccessibleBodyTextBlock");
        keys.Should().Contain("AccessibleButton");
    }
}
