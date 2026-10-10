using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;

namespace TidyUp.Controls;

/// <summary>
/// Interaction logic for StatusCard.xaml
/// </summary>
public partial class StatusCard : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(StatusCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(string), typeof(StatusCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubtitleProperty =
        DependencyProperty.Register(nameof(Subtitle), typeof(string), typeof(StatusCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty IconProperty =
        DependencyProperty.Register(nameof(Icon), typeof(PackIconKind), typeof(StatusCard), new PropertyMetadata(PackIconKind.Information));

    public static readonly DependencyProperty IconForegroundProperty =
        DependencyProperty.Register(nameof(IconForeground), typeof(Brush), typeof(StatusCard), new PropertyMetadata(Brushes.DodgerBlue));

    public static readonly DependencyProperty BadgeTextProperty =
        DependencyProperty.Register(nameof(BadgeText), typeof(string), typeof(StatusCard), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty BadgeBackgroundProperty =
        DependencyProperty.Register(nameof(BadgeBackground), typeof(Brush), typeof(StatusCard), new PropertyMetadata(Brushes.Transparent));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Value
    {
        get => (string)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public string Subtitle
    {
        get => (string)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public PackIconKind Icon
    {
        get => (PackIconKind)GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public Brush IconForeground
    {
        get => (Brush)GetValue(IconForegroundProperty);
        set => SetValue(IconForegroundProperty, value);
    }

    public string BadgeText
    {
        get => (string)GetValue(BadgeTextProperty);
        set => SetValue(BadgeTextProperty, value);
    }

    public Brush BadgeBackground
    {
        get => (Brush)GetValue(BadgeBackgroundProperty);
        set => SetValue(BadgeBackgroundProperty, value);
    }

    public StatusCard()
    {
        InitializeComponent();
    }
}

