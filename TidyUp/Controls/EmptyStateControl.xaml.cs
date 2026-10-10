using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TidyUp.Controls;

/// <summary>
/// Interaction logic for EmptyStateControl.xaml
/// Provides user-friendly empty states for lists, tables, and search results.
/// </summary>
public partial class EmptyStateControl : UserControl
{
    public static readonly DependencyProperty IconKindProperty =
        DependencyProperty.Register(
            nameof(IconKind),
            typeof(string),
            typeof(EmptyStateControl),
            new PropertyMetadata("InformationOutline"));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(
            nameof(Title),
            typeof(string),
            typeof(EmptyStateControl),
            new PropertyMetadata("No Items Found"));

    public static readonly DependencyProperty DescriptionProperty =
        DependencyProperty.Register(
            nameof(Description),
            typeof(string),
            typeof(EmptyStateControl),
            new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ActionTextProperty =
        DependencyProperty.Register(
            nameof(ActionText),
            typeof(string),
            typeof(EmptyStateControl),
            new PropertyMetadata(string.Empty, OnActionTextChanged));

    public static readonly DependencyProperty ActionCommandProperty =
        DependencyProperty.Register(
            nameof(ActionCommand),
            typeof(ICommand),
            typeof(EmptyStateControl),
            new PropertyMetadata(null, OnActionCommandChanged));

    public static readonly DependencyProperty ActionCommandParameterProperty =
        DependencyProperty.Register(
            nameof(ActionCommandParameter),
            typeof(object),
            typeof(EmptyStateControl),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ActionVisibilityProperty =
        DependencyProperty.Register(
            nameof(ActionVisibility),
            typeof(Visibility),
            typeof(EmptyStateControl),
            new PropertyMetadata(Visibility.Collapsed));

    public string IconKind
    {
        get => (string)GetValue(IconKindProperty);
        set => SetValue(IconKindProperty, value);
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    public string ActionText
    {
        get => (string)GetValue(ActionTextProperty);
        set => SetValue(ActionTextProperty, value);
    }

    public ICommand? ActionCommand
    {
        get => (ICommand?)GetValue(ActionCommandProperty);
        set => SetValue(ActionCommandProperty, value);
    }

    public object? ActionCommandParameter
    {
        get => GetValue(ActionCommandParameterProperty);
        set => SetValue(ActionCommandParameterProperty, value);
    }

    public Visibility ActionVisibility
    {
        get => (Visibility)GetValue(ActionVisibilityProperty);
        set => SetValue(ActionVisibilityProperty, value);
    }

    public EmptyStateControl()
    {
        InitializeComponent();
    }

    private static void OnActionTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EmptyStateControl control)
        {
            control.UpdateActionVisibility();
        }
    }

    private static void OnActionCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EmptyStateControl control)
        {
            control.UpdateActionVisibility();
        }
    }

    private void UpdateActionVisibility()
    {
        ActionVisibility = !string.IsNullOrWhiteSpace(ActionText) && ActionCommand != null
            ? Visibility.Visible
            : Visibility.Collapsed;
    }
}

