using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace TidyUp.Controls;

/// <summary>
/// The title block every destination starts with: one title size, one subtitle style, and a right-aligned
/// slot (<see cref="Actions"/>) for the page's existing buttons and chips.
/// </summary>
[ContentProperty(nameof(Actions))]
public partial class PageHeader : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(PageHeader), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(PageHeader),
            new PropertyMetadata(string.Empty, (d, e) => ((PageHeader)d).UpdateSubtitleVisibility()));

    public static readonly DependencyProperty ActionsProperty =
        DependencyProperty.Register(nameof(Actions), typeof(object), typeof(PageHeader), new PropertyMetadata(null));

    public PageHeader()
    {
        InitializeComponent();
        UpdateSubtitleVisibility();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    /// <summary>Content shown at the right edge, typically a horizontal StackPanel of buttons.</summary>
    public object? Actions
    {
        get => GetValue(ActionsProperty);
        set => SetValue(ActionsProperty, value);
    }

    private void UpdateSubtitleVisibility()
    {
        SubtitleText.Visibility = string.IsNullOrWhiteSpace(Subtitle) ? Visibility.Collapsed : Visibility.Visible;
    }
}
