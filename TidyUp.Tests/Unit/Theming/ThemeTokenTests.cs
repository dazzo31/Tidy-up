using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Xml.Linq;
using FluentAssertions;
using TidyUp.Styles;
using TidyUp.Tests.TestInfrastructure;
using Xunit;

namespace TidyUp.Tests.Unit.Theming;

public class ThemeTokenTests
{
    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static TheoryData<string> Palettes => new() { "Styles/Colors.xaml", "Styles/Colors.Dark.xaml" };

    private static IEnumerable<string> AllTokens() =>
        typeof(ThemeTokens).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!);

    [Theory]
    [MemberData(nameof(Palettes))]
    public void EveryTokenConstant_ExistsInThePalette(string palette)
    {
        var keys = XDocument.Load(AppSourceFiles.Find(palette)).Descendants()
            .Select(e => (string?)e.Attribute(Xaml + "Key")).Where(k => k != null).ToHashSet();

        AllTokens().Where(t => !keys.Contains(t)).Should().BeEmpty("a token missing from a palette would render as no colour at all");
    }

    [Fact]
    public void TokenConstants_AreUnique()
    {
        AllTokens().Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void ThemeBrush_Background_FollowsTheResourceWhenItIsReplaced()
    {
        Sta.Run(() =>
        {
            var border = new Border();
            border.Resources[ThemeTokens.SuccessBackground] = new SolidColorBrush(Colors.White);

            ThemeBrush.SetBackground(border, ThemeTokens.SuccessBackground);
            ((SolidColorBrush)border.Background).Color.Should().Be(Colors.White);

            // What a theme switch does: swap the resource. A one-off binding would keep the old colour.
            border.Resources[ThemeTokens.SuccessBackground] = new SolidColorBrush(Colors.Black);
            ((SolidColorBrush)border.Background).Color.Should().Be(Colors.Black);
        });
    }

    [Fact]
    public void ThemeBrush_Background_FollowsAChangeOfKey_AndClearsOnNull()
    {
        Sta.Run(() =>
        {
            var border = new Border();
            border.Resources[ThemeTokens.Error] = new SolidColorBrush(Colors.Red);
            border.Resources[ThemeTokens.Info] = new SolidColorBrush(Colors.Blue);

            ThemeBrush.SetBackground(border, ThemeTokens.Error);
            ((SolidColorBrush)border.Background).Color.Should().Be(Colors.Red);

            ThemeBrush.SetBackground(border, ThemeTokens.Info);
            ((SolidColorBrush)border.Background).Color.Should().Be(Colors.Blue);

            ThemeBrush.SetBackground(border, null);
            border.Background.Should().BeNull();
        });
    }

    [Fact]
    public void ThemeBrush_Foreground_AppliesToTextBlocksAndControls()
    {
        Sta.Run(() =>
        {
            var text = new TextBlock();
            text.Resources[ThemeTokens.Warning] = new SolidColorBrush(Colors.Orange);
            ThemeBrush.SetForeground(text, ThemeTokens.Warning);
            ((SolidColorBrush)text.Foreground).Color.Should().Be(Colors.Orange);

            var control = new Button();
            control.Resources[ThemeTokens.Warning] = new SolidColorBrush(Colors.Orange);
            ThemeBrush.SetForeground(control, ThemeTokens.Warning);
            ((SolidColorBrush)control.Foreground).Color.Should().Be(Colors.Orange);
        });
    }

    [Fact]
    public void ThemeBrush_Background_AppliesToPanelsAndControls_AndIgnoresUnsupportedElements()
    {
        Sta.Run(() =>
        {
            var grid = new Grid();
            grid.Resources[ThemeTokens.Surface] = new SolidColorBrush(Colors.Gray);
            ThemeBrush.SetBackground(grid, ThemeTokens.Surface);
            ((SolidColorBrush)grid.Background).Color.Should().Be(Colors.Gray);

            var button = new Button();
            button.Resources[ThemeTokens.Surface] = new SolidColorBrush(Colors.Gray);
            ThemeBrush.SetBackground(button, ThemeTokens.Surface);
            ((SolidColorBrush)button.Background).Color.Should().Be(Colors.Gray);

            var act = () => ThemeBrush.SetBackground(new TextBlock(), ThemeTokens.Surface);
            act.Should().NotThrow("TextBlock has no Background property; the attached value is simply inert");
        });
    }
}
