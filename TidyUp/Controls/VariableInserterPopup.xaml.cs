using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace TidyUp.Controls;

public partial class VariableInserterPopup : UserControl
{
    public event EventHandler<string>? VariableSelected;

    public VariableInserterPopup()
    {
        InitializeComponent();
        LoadVariables();
    }

    private void LoadVariables()
    {
        var variables = new[]
        {
            new { Variable = "{filename}", Description = "File name without extension" },
            new { Variable = "{extension}", Description = "File extension (without dot)" },
            new { Variable = "{fullname}", Description = "Complete filename with extension" },
            new { Variable = "{filesize}", Description = "Size in bytes" },
            new { Variable = "{filesize_kb}", Description = "Size in kilobytes" },
            new { Variable = "{filesize_mb}", Description = "Size in megabytes" },
            new { Variable = "{created_date}", Description = "Created date (YYYY-MM-DD)" },
            new { Variable = "{modified_date}", Description = "Modified date (YYYY-MM-DD)" },
            new { Variable = "{created_year}", Description = "Created year (YYYY)" },
            new { Variable = "{created_month}", Description = "Created month (MM)" },
            new { Variable = "{created_day}", Description = "Created day (DD)" },
            new { Variable = "{modified_year}", Description = "Modified year (YYYY)" },
            new { Variable = "{modified_month}", Description = "Modified month (MM)" },
            new { Variable = "{modified_day}", Description = "Modified day (DD)" },
            new { Variable = "{folder_path}", Description = "Parent folder full path" },
            new { Variable = "{folder_name}", Description = "Parent folder name only" },
            new { Variable = "{counter}", Description = "Auto-incrementing number" },
            new { Variable = "{counter:000}", Description = "Counter with padding (001, 002, etc.)" },
            new { Variable = "{filename:upper}", Description = "Filename in UPPERCASE" },
            new { Variable = "{filename:lower}", Description = "Filename in lowercase" },
            new { Variable = "{date:yyyyMMdd}", Description = "Current date in custom format" }
        };

        VariablesList.ItemsSource = variables;
    }

    private void VariableButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string variable)
        {
            VariableSelected?.Invoke(this, variable);
            
            // Close the popup
            var popup = FindParent<Popup>(this);
            if (popup != null)
            {
                popup.IsOpen = false;
            }
        }
    }

    private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
    {
        var parent = LogicalTreeHelper.GetParent(child);
        
        if (parent == null)
            return null;
        
        if (parent is T typedParent)
            return typedParent;
        
        return FindParent<T>(parent);
    }
}
