using FluentAssertions;
using TidyUp.Tests.TestInfrastructure;
using Xunit;

namespace TidyUp.Tests.Unit.Theming;

/// <summary>
/// Settings used to be one 850 px column, so a wide window wasted two thirds of the space.
/// </summary>
public class ResponsiveLayoutTests
{
    [Fact]
    public void Settings_ReflowsItsCards_InsteadOfOneFixedWidthColumn()
    {
        var xaml = File.ReadAllText(AppSourceFiles.Find("Views/SettingsView.xaml"));

        xaml.Should().NotContain("MaxWidth=\"850\"", "a fixed column cannot reflow");
        xaml.Should().Contain("<WrapPanel", "cards wrap into as many columns as fit");
    }

    [Fact]
    public void Settings_CardsShareOneWidth_SoTheGridStaysEven()
    {
        var xaml = File.ReadAllText(AppSourceFiles.Find("Views/SettingsView.xaml"));
        var cardCount = xaml.Split("<materialDesign:Card Width=\"520\"").Length - 1;

        cardCount.Should().BeGreaterThanOrEqualTo(10, "every settings card should participate in the wrap layout");
    }

    [Fact]
    public void Settings_DisablesHorizontalScrolling_SoContentNeverOverflowsSideways()
    {
        File.ReadAllText(AppSourceFiles.Find("Views/SettingsView.xaml"))
            .Should().Contain("HorizontalScrollBarVisibility=\"Disabled\"");
    }
}
