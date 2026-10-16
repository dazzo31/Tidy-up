using FluentAssertions;
using TidyUp.Models.Folders;
using TidyUp.Services.Folders;
using TidyUp.ViewModels;
using Xunit;

namespace TidyUp.Tests.Unit.Services;

public class TransientLocationsTests
{
    private static string TempRoot => Path.TrimEndingDirectorySeparator(Path.GetTempPath());

    [Fact]
    public void TempFolderItself_IsTransient()
    {
        TransientLocations.IsTransient(TempRoot).Should().BeTrue();
    }

    [Fact]
    public void FolderInsideTemp_IsTransient_RegardlessOfCaseOrTrailingSlash()
    {
        var leaf = Path.Combine(TempRoot, "TidyUp_UiIntegration_abc", "Downloads");

        TransientLocations.IsTransient(leaf).Should().BeTrue();
        TransientLocations.IsTransient(leaf.ToUpperInvariant() + Path.DirectorySeparatorChar).Should().BeTrue();
    }

    [Fact]
    public void SiblingSharingTheTempPrefix_IsNotTransient()
    {
        TransientLocations.IsTransient(TempRoot + "2").Should().BeFalse();
        TransientLocations.IsTransient(TempRoot + "Archive").Should().BeFalse();
    }

    [Fact]
    public void OrdinaryUserFolder_IsNotTransient()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        TransientLocations.IsTransient(documents).Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("bad\0path")]
    public void EmptyOrInvalidPaths_AreNotTransient(string? path)
    {
        TransientLocations.IsTransient(path).Should().BeFalse();
    }

    [Fact]
    public void CatalogState_ExposesTheFlagFromItsNormalizedPath()
    {
        new MonitoredFolderState { NormalizedPath = Path.Combine(TempRoot, "x") }.IsTransientLocation.Should().BeTrue();
        new MonitoredFolderState { NormalizedPath = @"C:\Users\Someone\Documents" }.IsTransientLocation.Should().BeFalse();
    }

    [Fact]
    public void FolderItemViewModel_UpdatesTheFlagWhenItsPathChanges()
    {
        var item = new MonitoredFolderItemViewModel { Path = @"C:\Users\Someone\Documents" };
        var raised = new List<string?>();
        item.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        item.IsTransientLocation.Should().BeFalse();
        item.Path = Path.Combine(TempRoot, "x");

        item.IsTransientLocation.Should().BeTrue();
        raised.Should().Contain(nameof(MonitoredFolderItemViewModel.IsTransientLocation));
    }
}
