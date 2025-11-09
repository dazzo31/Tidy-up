using System.Globalization;
using System.Windows.Data;
using TidyUp.Models.Domain;

namespace TidyUp.Converters;

/// <summary>
/// Converts a FileAction to its display name.
/// </summary>
public class ActionTypeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value switch
        {
            MoveFileAction => "Move File",
            CopyFileAction => "Copy File",
            RenameFileAction => "Rename File",
            ChangeExtensionAction => "Change Extension",
            DeleteFileAction => "Delete File",
            ExtractArchiveAction => "Extract Archive",
            RunCommandAction => "Run Command",
            _ => "Unknown Action"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
