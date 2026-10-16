using System.Globalization;
using System.Xml.Linq;
using FluentAssertions;
using TidyUp.Tests.TestInfrastructure;
using Xunit;

namespace TidyUp.Tests.Unit.Theming;

/// <summary>
/// The palettes are verified from the XAML itself. The dark theme can't be eyeballed in CI,
/// so readability is enforced as WCAG AA contrast (4.5:1) on every text/background pair the tokens are used for.
/// </summary>
public class ThemePaletteTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";
    private static readonly XNamespace Presentation = "http://schemas.microsoft.com/winfx/2006/xaml/presentation";

    private static readonly (string Foreground, string Background)[] ReadablePairs =
    [
        ("Win11TextPrimaryBrush", "Win11SurfaceBrush"),
        ("Win11TextPrimaryBrush", "Win11SubtleSurfaceBrush"),
        ("Win11TextSecondaryBrush", "Win11SurfaceBrush"),
        ("Win11TextSecondaryBrush", "Win11SubtleSurfaceBrush"),
        ("Win11AccentBrush", "Win11SurfaceBrush"),
        ("Win11AccentBrush", "Win11AccentLightBrush"),
        ("Win11SuccessBrush", "Win11SurfaceBrush"),
        ("Win11SuccessBrush", "Win11SuccessBackgroundBrush"),
        ("Win11WarningBrush", "Win11SurfaceBrush"),
        ("Win11WarningBrush", "Win11SubtleSurfaceBrush"),
        ("Win11WarningBrush", "Win11WarningBackgroundBrush"),
        ("Win11ErrorBrush", "Win11SurfaceBrush"),
        ("Win11ErrorBrush", "Win11ErrorBackgroundBrush"),
        ("Win11InfoBrush", "Win11SurfaceBrush"),
        ("Win11InfoBrush", "Win11InfoBackgroundBrush")
    ];

    public static TheoryData<string> Palettes => new() { "Styles/Colors.xaml", "Styles/Colors.Dark.xaml" };

    [Fact]
    public void DarkPalette_DefinesExactlyTheKeysOfTheLightPalette()
    {
        var light = Keys("Styles/Colors.xaml");
        var dark = Keys("Styles/Colors.Dark.xaml");

        dark.Should().BeEquivalentTo(light, "the dark overlay replaces keys one-for-one; a missing key would stay light and an extra one would be dead");
    }

    [Theory]
    [MemberData(nameof(Palettes))]
    public void EveryReadablePair_MeetsWcagAa(string palette)
    {
        var colors = ResolveBrushColors(palette);

        var failures = ReadablePairs
            .Select(p => (p.Foreground, p.Background, Ratio: ContrastRatio(colors[p.Foreground], colors[p.Background])))
            .Where(x => x.Ratio < 4.5)
            .Select(x => $"{x.Foreground} on {x.Background} = {x.Ratio:0.00}:1")
            .ToList();

        failures.Should().BeEmpty();
    }

    [Fact]
    public void ContrastRatio_MatchesKnownValues()
    {
        ContrastRatio("#000000", "#FFFFFF").Should().BeApproximately(21.0, 0.01);
        ContrastRatio("#777777", "#FFFFFF").Should().BeApproximately(4.48, 0.01);
    }

    private static HashSet<string> Keys(string relativePath) =>
        XDocument.Load(AppSourceFiles.Find(relativePath))
            .Descendants()
            .Select(e => (string?)e.Attribute(Xaml + "Key"))
            .Where(k => k != null)
            .Select(k => k!)
            .ToHashSet();

    private static Dictionary<string, string> ResolveBrushColors(string relativePath)
    {
        var root = XDocument.Load(AppSourceFiles.Find(relativePath)).Root!;
        var colors = root.Elements(Presentation + "Color")
            .ToDictionary(e => (string)e.Attribute(Xaml + "Key")!, e => e.Value.Trim());

        var brushes = new Dictionary<string, string>();
        foreach (var brush in root.Elements(Presentation + "SolidColorBrush"))
        {
            var value = (string)brush.Attribute("Color")!;
            const string prefix = "{StaticResource ";
            brushes[(string)brush.Attribute(Xaml + "Key")!] =
                value.StartsWith(prefix, StringComparison.Ordinal) ? colors[value[prefix.Length..^1]] : value;
        }

        return brushes;
    }

    private static double ContrastRatio(string foreground, string background)
    {
        var a = RelativeLuminance(foreground);
        var b = RelativeLuminance(background);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double RelativeLuminance(string hex)
    {
        static double Channel(string h, int start)
        {
            var c = int.Parse(h.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }

        var h = hex.TrimStart('#');
        return 0.2126 * Channel(h, 0) + 0.7152 * Channel(h, 2) + 0.0722 * Channel(h, 4);
    }
}
