using FluentAssertions;
using TidyUp.Tests.TestInfrastructure;
using Xunit;

namespace TidyUp.Tests.Unit.Branding;

/// <summary>
/// The icon is read by Explorer, the taskbar, Alt-Tab, the title bar and the tray at different sizes.
/// A single-image ICO gets scaled by Windows and looks soft or wrong at 16 and 32 px.
/// </summary>
public class BrandAssetTests
{
    private static string IconPath =>
        Path.Combine(Path.GetDirectoryName(AppSourceFiles.Find("TidyUp.csproj"))!, "Resources", "tidyup.ico");

    private static List<(int Width, int Height, int Bpp)> ReadIconDirectory()
    {
        var bytes = File.ReadAllBytes(IconPath);
        bytes.Length.Should().BeGreaterThan(6);
        BitConverter.ToUInt16(bytes, 0).Should().Be(0, "ICO header reserved field");
        BitConverter.ToUInt16(bytes, 2).Should().Be(1, "type 1 = icon");

        var count = BitConverter.ToUInt16(bytes, 4);
        return Enumerable.Range(0, count)
            .Select(i =>
            {
                var o = 6 + 16 * i;
                // 0 in the width/height byte means 256.
                return (bytes[o] == 0 ? 256 : bytes[o], bytes[o + 1] == 0 ? 256 : bytes[o + 1], (int)BitConverter.ToUInt16(bytes, o + 6));
            })
            .ToList();
    }

    [Fact]
    public void Icon_ContainsAtLeastSixResolutions()
    {
        ReadIconDirectory().Should().HaveCountGreaterThanOrEqualTo(6);
    }

    [Theory]
    [InlineData(16)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(256)]
    public void Icon_ContainsTheSizesWindowsRequests(int size)
    {
        ReadIconDirectory().Should().Contain(e => e.Width == size && e.Height == size);
    }

    [Fact]
    public void Icon_ImagesAre32BitWithAlpha()
    {
        ReadIconDirectory().Should().OnlyContain(e => e.Bpp == 32, "rounded corners need an alpha channel");
    }

    [Fact]
    public void Project_EmbedsTheIconInTheExecutable()
    {
        var csproj = File.ReadAllText(AppSourceFiles.Find("TidyUp.csproj"));
        csproj.Should().Contain("<ApplicationIcon>Resources\\tidyup.ico</ApplicationIcon>");
    }

    [Fact]
    public void LogoControl_TakesItsColoursFromThemeTokens()
    {
        var xaml = File.ReadAllText(AppSourceFiles.Find("Controls/TidyUpLogo.xaml"));
        xaml.Should().Contain("Win11AccentBrush").And.Contain("Win11SurfaceBrush");
    }
}
