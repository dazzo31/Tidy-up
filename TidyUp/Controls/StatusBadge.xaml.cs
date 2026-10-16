using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using TidyUp.Models.Enums;
using TidyUp.Models.Folders;

namespace TidyUp.Controls;

/// <summary>
/// A pill showing a folder's <see cref="FolderStatus"/> with the same icon, wording and colours everywhere.
/// Optional <see cref="Detail"/> (for example the watcher's last error) is appended to the tooltip.
/// </summary>
public partial class StatusBadge : UserControl
{
    public static readonly DependencyProperty StatusProperty = DependencyProperty.Register(
        nameof(Status), typeof(FolderStatus), typeof(StatusBadge),
        new PropertyMetadata(FolderStatus.Healthy, (d, _) => ((StatusBadge)d).Refresh()));

    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(
        nameof(Detail), typeof(string), typeof(StatusBadge),
        new PropertyMetadata(null, (d, _) => ((StatusBadge)d).Refresh()));

    public StatusBadge()
    {
        InitializeComponent();
        Refresh();
    }

    public FolderStatus Status
    {
        get => (FolderStatus)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public string? Detail
    {
        get => (string?)GetValue(DetailProperty);
        set => SetValue(DetailProperty, value);
    }

    private void Refresh()
    {
        var status = Status;
        var text = FolderStatusPresentation.Text(status);
        var description = FolderStatusPresentation.Description(status);

        Pill.SetResourceReference(Border.BackgroundProperty, FolderStatusPresentation.BackgroundToken(status));
        Glyph.Kind = FolderStatusPresentation.Icon(status);
        Glyph.SetResourceReference(Control.ForegroundProperty, FolderStatusPresentation.ForegroundToken(status));
        Label.SetResourceReference(TextElement.ForegroundProperty, FolderStatusPresentation.ForegroundToken(status));
        Label.Text = text;

        ToolTip = string.IsNullOrWhiteSpace(Detail) ? description : $"{description}\n{Detail}";
        AutomationProperties.SetName(this, $"Status: {text}");
    }
}
