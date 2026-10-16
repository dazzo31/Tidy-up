using System.Text.RegularExpressions;
using FluentAssertions;
using TidyUp.Tests.TestInfrastructure;
using Xunit;

namespace TidyUp.Tests.Unit.Theming;

/// <summary>
/// Views must take colours from the theme tokens, otherwise Dark mode leaves pale fills under light text.
/// Fails with the exact file:line of every literal colour.
/// </summary>
public class StyleGuardTests
{
    private const string NamedColors = "Green|Red|Orange|White|Black|Gray|Grey|Blue|Yellow|Purple|Teal|Silver|Maroon|Navy|Lime|Cyan|Magenta";
    private const string Literal = "(#[0-9A-Fa-f]{3,8}|" + NamedColors + ")";
    private const string Quote = "\"";

    private static readonly Regex XamlLiteralColor = new(
        // Foreground="Green"   Background="#E8F5E9"
        "(Foreground|Background|BorderBrush|Fill|Stroke|IconForeground)\\s*=\\s*" + Quote + Literal + Quote
        // <Setter Property="Foreground" Value="#C62828"/>
        + "|Property=" + Quote + "(Foreground|Background|BorderBrush|Fill)" + Quote + "\\s+Value=" + Quote + Literal + Quote
        // <SolidColorBrush Color="#FFF3E0"/>
        + "|<SolidColorBrush\\s+Color=" + Quote + "#",
        RegexOptions.Compiled);

    private static readonly Regex CodeLiteralColor = new(
        Quote + "#[0-9A-Fa-f]{6,8}" + Quote + "|Color\\.From(Rgb|Argb)\\(|Brushes\\.",
        RegexOptions.Compiled);

    // Files that legitimately own a literal colour. Keep this list short and justified.
    private static readonly string[] AllowedCodeFiles =
    [
        "TrayIconService.cs" // paints a System.Drawing tray icon; it cannot take a WPF resource
    ];

    [Fact]
    public void Xaml_UsesThemeTokens_NotLiteralColours()
    {
        var offenders = Directory.EnumerateFiles(AppDir("Views"), "*.xaml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(AppDir("Controls"), "*.xaml", SearchOption.AllDirectories))
            .Append(AppSourceFiles.Find("MainWindow.xaml"))
            .SelectMany(file => Matches(file, XamlLiteralColor))
            .ToList();

        offenders.Should().BeEmpty(
            "literal colours stay light in Dark mode; use a DynamicResource token from Colors.xaml (or ThemeBrush for view-model values)");
    }

    [Fact]
    public void ViewModelsAndModels_ReturnThemeTokenKeys_NotColours()
    {
        var offenders = new[] { "ViewModels", "Models" }
            .SelectMany(d => Directory.EnumerateFiles(AppDir(d), "*.cs", SearchOption.AllDirectories))
            .Where(f => !AllowedCodeFiles.Contains(Path.GetFileName(f)))
            .SelectMany(file => Matches(file, CodeLiteralColor))
            .ToList();

        offenders.Should().BeEmpty("view-models should return a ThemeTokens key so the badge follows the active theme");
    }

    [Fact]
    public void Guard_DetectsLiteralColoursInTheShapesTheViewsUse()
    {
        // The guard is only worth having if it fires on every pattern found in the views.
        var samples = new[]
        {
            """<Border Background="#E8F5E9"/>""",
            """<TextBlock Foreground="Green"/>""",
            """<Setter Property="Foreground" Value="#C62828"/>""",
            """<materialDesign:PackIcon Foreground="White"/>""",
            """<Ellipse Fill="#FFF3E0"/>""",
            """<SolidColorBrush Color="#FFF3E0"/>"""
        };

        foreach (var sample in samples)
        {
            XamlLiteralColor.IsMatch(sample).Should().BeTrue($"should flag: {sample}");
        }

        XamlLiteralColor.IsMatch("""<Border Background="{DynamicResource Win11SuccessBackgroundBrush}"/>""").Should().BeFalse();
        XamlLiteralColor.IsMatch("""<Border Background="Transparent"/>""").Should().BeFalse();
        XamlLiteralColor.IsMatch("""<Setter Property="Background" Value="{DynamicResource Win11SurfaceBrush}"/>""").Should().BeFalse();
    }

    private static string AppDir(string folder) =>
        Path.Combine(Path.GetDirectoryName(AppSourceFiles.Find("TidyUp.csproj"))!, folder);

    private static IEnumerable<string> Matches(string file, Regex pattern) =>
        File.ReadAllLines(file)
            .Select((text, i) => (text, line: i + 1))
            .Where(x => pattern.IsMatch(x.text))
            .Select(x => $"{Path.GetFileName(file)}:{x.line}: {x.text.Trim()}");
}
