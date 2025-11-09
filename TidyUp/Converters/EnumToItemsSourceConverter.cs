using System.Globalization;
using System.Windows.Data;

namespace TidyUp.Converters;

/// <summary>
/// Converts an enum type to a collection of its values for binding to ItemsSource.
/// </summary>
public class EnumToItemsSourceConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Type enumType && enumType.IsEnum)
        {
            return Enum.GetValues(enumType);
        }
        
        return Array.Empty<object>();
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
