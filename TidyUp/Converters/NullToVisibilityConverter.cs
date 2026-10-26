using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TidyUp.Converters;

/// <summary>
/// Converts an object reference to Visibility: returns Visible when not null, Collapsed when null.
/// </summary>
public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value != null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
