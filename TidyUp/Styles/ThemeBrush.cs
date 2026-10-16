using System.Windows;
using System.Windows.Controls;

namespace TidyUp.Styles;

/// <summary>
/// Binds a theme-token key (see <see cref="ThemeTokens"/>) to a brush property as a live resource reference.
/// A plain binding to a colour is evaluated once; this one re-resolves when the theme dictionary changes.
/// <code>&lt;Border styles:ThemeBrush.Background="{Binding BadgeBackground}"/&gt;</code>
/// </summary>
public static class ThemeBrush
{
    public static readonly DependencyProperty BackgroundProperty = DependencyProperty.RegisterAttached(
        "Background", typeof(string), typeof(ThemeBrush),
        new PropertyMetadata(null, (d, e) => Apply(d, BackgroundTarget(d), e.NewValue as string)));

    public static readonly DependencyProperty ForegroundProperty = DependencyProperty.RegisterAttached(
        "Foreground", typeof(string), typeof(ThemeBrush),
        new PropertyMetadata(null, (d, e) => Apply(d, System.Windows.Documents.TextElement.ForegroundProperty, e.NewValue as string)));

    public static string? GetBackground(DependencyObject element) => (string?)element.GetValue(BackgroundProperty);
    public static void SetBackground(DependencyObject element, string? key) => element.SetValue(BackgroundProperty, key);

    public static string? GetForeground(DependencyObject element) => (string?)element.GetValue(ForegroundProperty);
    public static void SetForeground(DependencyObject element, string? key) => element.SetValue(ForegroundProperty, key);

    // Border, Panel and Control each declare their own Background property.
    private static DependencyProperty? BackgroundTarget(DependencyObject element) => element switch
    {
        Border => Border.BackgroundProperty,
        Panel => Panel.BackgroundProperty,
        Control => Control.BackgroundProperty,
        _ => null
    };

    private static void Apply(DependencyObject element, DependencyProperty? target, string? key)
    {
        if (element is not FrameworkElement frameworkElement || target is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(key))
        {
            frameworkElement.ClearValue(target);
        }
        else
        {
            frameworkElement.SetResourceReference(target, key);
        }
    }
}
