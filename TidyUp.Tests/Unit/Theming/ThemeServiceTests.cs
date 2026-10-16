using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using FluentAssertions;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using TidyUp.Models;
using TidyUp.Services.Theming;
using TidyUp.Tests.TestInfrastructure;
using Xunit;

namespace TidyUp.Tests.Unit.Theming;

public class ThemeServiceTests
{
    private const string LightSurface = "#FFFFFFFF";
    private const string DarkSurface = "#FF202020";

    private sealed class Harness
    {
        public ResourceDictionary Resources { get; } = new();
        public BundledTheme Bundled { get; } = new()
        {
            BaseTheme = BaseTheme.Light,
            PrimaryColor = PrimaryColor.Blue,
            SecondaryColor = SecondaryColor.DeepOrange
        };

        public bool SystemUsesLight { get; set; } = true;
        public int DarkTokenLoads { get; private set; }
        public ThemeService Service { get; }

        public Harness()
        {
            Resources.MergedDictionaries.Add(Bundled);
            Resources.MergedDictionaries.Add(Load("Styles/Colors.xaml"));

            Service = new ThemeService(
                Resources,
                () =>
                {
                    DarkTokenLoads++;
                    return Load("Styles/Colors.Dark.xaml");
                },
                () => SystemUsesLight);
        }

        public string Surface() => ((SolidColorBrush)Resources["Win11SurfaceBrush"]).Color.ToString();

        private static ResourceDictionary Load(string relativePath)
        {
            using var stream = File.OpenRead(AppSourceFiles.Find(relativePath));
            return (ResourceDictionary)XamlReader.Load(stream);
        }
    }

    [Fact]
    public void Apply_Dark_SwitchesMaterialBaseThemeAndOverlaysDarkTokens()
    {
        Sta.Run(() =>
        {
            var h = new Harness();
            h.Surface().Should().Be(LightSurface);

            h.Service.Apply("Dark");

            h.Bundled.BaseTheme.Should().Be(BaseTheme.Dark);
            h.Surface().Should().Be(DarkSurface);
            h.Service.EffectiveTheme.Should().Be("Dark");
        });
    }

    [Fact]
    public void Apply_LightAfterDark_RestoresLightTokensAndRemovesTheOverlay()
    {
        Sta.Run(() =>
        {
            var h = new Harness();
            var baseline = h.Resources.MergedDictionaries.Count;
            h.Service.Apply("Dark");

            h.Service.Apply("Light");

            h.Bundled.BaseTheme.Should().Be(BaseTheme.Light);
            h.Surface().Should().Be(LightSurface);
            h.Resources.MergedDictionaries.Should().HaveCount(baseline);
            h.Service.EffectiveTheme.Should().Be("Light");
        });
    }

    [Fact]
    public void Apply_DarkRepeatedly_LoadsAndMergesTheOverlayOnce()
    {
        Sta.Run(() =>
        {
            var h = new Harness();
            var baseline = h.Resources.MergedDictionaries.Count;

            h.Service.Apply("Dark");
            h.Service.Apply("Dark");

            h.DarkTokenLoads.Should().Be(1);
            h.Resources.MergedDictionaries.Should().HaveCount(baseline + 1);
        });
    }

    [Theory]
    [InlineData(true, "Light")]
    [InlineData(false, "Dark")]
    public void Apply_System_FollowsTheWindowsPreference(bool systemUsesLight, string expected)
    {
        Sta.Run(() =>
        {
            var h = new Harness { SystemUsesLight = systemUsesLight };

            h.Service.Apply("System");

            h.Service.EffectiveTheme.Should().Be(expected);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Purple")]
    public void Apply_UnrecognisedValue_FallsBackToSystem(string? value)
    {
        Sta.Run(() =>
        {
            var h = new Harness { SystemUsesLight = false };

            h.Service.Apply(value);

            h.Service.EffectiveTheme.Should().Be("Dark");
        });
    }

    [Fact]
    public void RefreshSystemTheme_FollowsWindowsOnlyWhileSystemIsSelected()
    {
        Sta.Run(() =>
        {
            var h = new Harness { SystemUsesLight = true };

            h.Service.Apply("System");
            h.SystemUsesLight = false;
            h.Service.RefreshSystemTheme();
            h.Service.EffectiveTheme.Should().Be("Dark");

            h.Service.Apply("Light");
            h.SystemUsesLight = false;
            h.Service.RefreshSystemTheme();
            h.Service.EffectiveTheme.Should().Be("Light", "an explicit choice must not be overridden by Windows");
        });
    }

    [Fact]
    public void Attach_AppliesImmediately_AndFollowsLaterThemeChanges()
    {
        Sta.Run(() =>
        {
            var h = new Harness();
            var settings = new AppSettings { Theme = "Dark" };

            h.Service.Attach(settings);
            h.Service.EffectiveTheme.Should().Be("Dark");

            settings.Theme = "Light";
            h.Service.EffectiveTheme.Should().Be("Light");
        });
    }

    [Fact]
    public void Attach_IgnoresOtherSettingsChanges_AndStopsFollowingThePreviousInstance()
    {
        Sta.Run(() =>
        {
            var h = new Harness();
            var first = new AppSettings { Theme = "Light" };
            var second = new AppSettings { Theme = "Light" };

            h.Service.Attach(first);
            h.Service.Attach(second);
            first.Theme = "Dark";
            second.ScanBatchSize = 10;

            h.Service.EffectiveTheme.Should().Be("Light");
        });
    }

    [Fact]
    public void Apply_WithoutABundledTheme_ThrowsAClearError()
    {
        Sta.Run(() =>
        {
            var service = new ThemeService(new ResourceDictionary(), () => new ResourceDictionary());

            var act = () => service.Apply("Dark");

            act.Should().Throw<InvalidOperationException>().WithMessage("*BundledTheme*");
        });
    }
}
