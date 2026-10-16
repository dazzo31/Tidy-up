using System.Xml.Linq;
using FluentAssertions;
using TidyUp.Tests.TestInfrastructure;
using Xunit;

namespace TidyUp.Tests.Unit.Theming;

/// <summary>
/// An empty filter box or combo with no visible hint is unlabeled to a sighted user, and has no name for a screen
/// reader. Every text input in Views/ and Controls/ must carry a hint or an automation name.
/// </summary>
public class InputLabelGuardTests
{
    private static readonly string[] InputTypes = ["TextBox", "ComboBox", "DatePicker"];
    private const string MaterialDesignNs = "http://materialdesigninxaml.net/winfx/xaml/themes";

    private static readonly string[] ReadOnlyAllowedNote = [];

    [Fact]
    public void EveryInput_HasAHintOrAnAutomationName()
    {
        var offenders = new List<string>();
        foreach (var file in XamlFiles())
        {
            var doc = XDocument.Load(file, LoadOptions.SetLineInfo);
            foreach (var element in doc.Descendants().Where(e => InputTypes.Contains(e.Name.LocalName)))
            {
                if (!IsLabelled(element))
                {
                    var line = ((System.Xml.IXmlLineInfo)element).LineNumber;
                    offenders.Add($"{Path.GetFileName(file)}:{line}: <{element.Name.LocalName}>");
                }
            }
        }

        offenders.Should().BeEmpty("add materialDesign:HintAssist.Hint and/or AutomationProperties.Name to each input");
    }

    [Fact]
    public void Guard_RecognisesLabelledAndUnlabelledInputs()
    {
        IsLabelled(XElement.Parse($"<TextBox xmlns:m=\"{MaterialDesignNs}\" m:HintAssist.Hint=\"Search\"/>")).Should().BeTrue();
        IsLabelled(XElement.Parse("<ComboBox AutomationProperties.Name=\"Rule\"/>")).Should().BeTrue();
        IsLabelled(XElement.Parse("<TextBox Text=\"{Binding X}\"/>")).Should().BeFalse();
        IsLabelled(XElement.Parse($"<ComboBox xmlns:m=\"{MaterialDesignNs}\" m:HintAssist.Hint=\"\"/>")).Should().BeFalse("an empty hint is no hint");
    }

    private static bool IsLabelled(XElement element) =>
        element.Attributes().Any(a =>
            (a.Name.LocalName is "HintAssist.Hint" or "AutomationProperties.Name") && !string.IsNullOrWhiteSpace(a.Value));

    private static IEnumerable<string> XamlFiles()
    {
        var appDir = Path.GetDirectoryName(AppSourceFiles.Find("TidyUp.csproj"))!;
        return new[] { "Views", "Controls" }
            .SelectMany(d => Directory.EnumerateFiles(Path.Combine(appDir, d), "*.xaml", SearchOption.AllDirectories));
    }
}
