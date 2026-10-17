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

    /// <summary>Default = nothing here yet; Error = something failed and the user may need to act.</summary>
    public static readonly DependencyProperty VariantProperty =
        DependencyProperty.Register(
            nameof(Variant),
            typeof(EmptyStateVariant),
            typeof(EmptyStateControl),
            new PropertyMetadata(EmptyStateVariant.Default, OnVariantChanged));

    /// <summary>Optional technical detail, shown in a monospace panel (error variant).</summary>
    public static readonly DependencyProperty DetailsProperty =
        DependencyProperty.Register(
            nameof(Details),
            typeof(string),
            typeof(EmptyStateControl),
            new PropertyMetadata(null, OnDetailsChanged));

    public static readonly DependencyProperty SecondaryActionTextProperty =
        DependencyProperty.Register(
            nameof(SecondaryActionText),
            typeof(string),
            typeof(EmptyStateControl),
            new PropertyMetadata(string.Empty, OnSecondaryActionChanged));

    public static readonly DependencyProperty SecondaryActionCommandProperty =
        DependencyProperty.Register(
            nameof(SecondaryActionCommand),
            typeof(ICommand),
            typeof(EmptyStateControl),
            new PropertyMetadata(null, OnSecondaryActionChanged));

    public EmptyStateVariant Variant
    {
        get => (EmptyStateVariant)GetValue(VariantProperty);
        set => SetValue(VariantProperty, value);
    }

    public string? Details
    {
        get => (string?)GetValue(DetailsProperty);
        set => SetValue(DetailsProperty, value);
    }

    public string SecondaryActionText
    {
        get => (string)GetValue(SecondaryActionTextProperty);
        set => SetValue(SecondaryActionTextProperty, value);
    }

    public ICommand? SecondaryActionCommand
    {
        get => (ICommand?)GetValue(SecondaryActionCommandProperty);
        set => SetValue(SecondaryActionCommandProperty, value);
    }

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
        ApplyVariant(Variant);
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

    private static void OnVariantChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EmptyStateControl control)
        {
            control.ApplyVariant((EmptyStateVariant)e.NewValue);
        }
    }

    private void ApplyVariant(EmptyStateVariant variant)
    {
        var isError = variant == EmptyStateVariant.Error;
        IconBadge.SetResourceReference(Border.BackgroundProperty,
            isError ? "Win11ErrorBackgroundBrush" : "MaterialDesignToolBarBackground");
        IconGlyph.SetResourceReference(System.Windows.Documents.TextElement.ForegroundProperty,
            isError ? "Win11ErrorBrush" : "PrimaryHueMidBrush");
    }

    private static void OnDetailsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EmptyStateControl control)
        {
            var details = e.NewValue as string;
            control.DetailsText.Text = details;
            control.DetailsPanel.Visibility = string.IsNullOrWhiteSpace(details) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    private static void OnSecondaryActionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is EmptyStateControl control)
        {
            control.SecondaryButton.Visibility = control.SecondaryActionCommand != null && !string.IsNullOrWhiteSpace(control.SecondaryActionText)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }
}

/// <summary>How the empty/error state should read.</summary>
public enum EmptyStateVariant
{
    Default,
    Error
}

