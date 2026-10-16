using FluentAssertions;
using TidyUp.Tests.TestInfrastructure;
using Xunit;

namespace TidyUp.Tests.Unit.Theming;

/// <summary>
/// Every destination starts with the same PageHeader so titles share one size and one position.
/// </summary>
public class PageHeaderTests
{
    public static TheoryData<string> Destinations => new()
    {
        "Views/DashboardView.xaml",
        "Views/FoldersView.xaml",
        "Views/PreviewView.xaml",
        "Views/HistoryView.xaml",
        "Views/LogsView.xaml",
        "Views/SettingsView.xaml"
    };

    [Theory]
    [MemberData(nameof(Destinations))]
    public void Destination_UsesPageHeader_NotAdHocTitleStyles(string view)
    {
        var xaml = File.ReadAllText(AppSourceFiles.Find(view));

        xaml.Should().Contain("<controls:PageHeader", "every destination uses the shared header");
        xaml.Should().NotContain("MaterialDesignHeadline4TextBlock").And.NotContain("MaterialDesignHeadline5TextBlock",
            "a second H1 style would break the single title size");
    }

    [Fact]
    public void PageTitleStyle_IsTheOnlyH1Size()
    {
        var typography = File.ReadAllText(AppSourceFiles.Find("Styles/Typography.xaml"));
        typography.Should().Contain("Win11PageTitleTextBlock").And.Contain("Win11PageSubtitleTextBlock");
    }
}
