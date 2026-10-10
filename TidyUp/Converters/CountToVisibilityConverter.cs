using System;
using System.Collections;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TidyUp.Converters;

/// <summary>
/// Converts an integer count or collection count into a Visibility value.
/// By default: Count > 0 => Visible, Count == 0 => Collapsed.
/// If ConverterParameter is "Zero", "Empty", or "Invert": Count == 0 => Visible, Count > 0 => Collapsed.
/// </summary>
public class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        int count = 0;

        if (value is int intVal)
        {
            count = intVal;
        }
        else if (value is long longVal)
        {
            count = (int)longVal;
        }
        else if (value is ICollection collection)
        {
            count = collection.Count;
        }
        else if (value is IEnumerable enumerable)
        {
            var enumerator = enumerable.GetEnumerator();
            count = enumerator.MoveNext() ? 1 : 0;
            if (enumerator is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        bool invert = parameter is string paramStr &&
                      (paramStr.Equals("Zero", StringComparison.OrdinalIgnoreCase) ||
                       paramStr.Equals("Empty", StringComparison.OrdinalIgnoreCase) ||
                       paramStr.Equals("Invert", StringComparison.OrdinalIgnoreCase));

        bool isVisible = invert ? count == 0 : count > 0;

        return isVisible ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

